/**
 * Parses a datetime string from backend (SQLite doesn't include 'Z' suffix)
 * and returns a proper Date object treating the input as UTC.
 */
export function parseUtcDate(
  dateString: string | null | undefined,
): Date | null {
  if (!dateString) return null;

  // If already has timezone indicator, parse as-is
  if (
    dateString.endsWith("Z") ||
    dateString.includes("+") ||
    /[+-]\d{2}:\d{2}$/.test(dateString)
  ) {
    return new Date(dateString);
  }

  // Append 'Z' to indicate UTC, then parse
  return new Date(dateString + "Z");
}

/**
 * Formats a date as a relative time string (e.g., "2 minutes ago", "in 5 minutes", "yesterday", "tomorrow")
 * Works for both past and future dates
 */
export function formatRelativeTime(date: Date | string | null | undefined, t: (key: string, options?: any) => string): string {
  if (!date) return t("common.never");
  
  const dateObj = typeof date === "string" ? parseUtcDate(date) : date;
  if (!dateObj) return t("common.never");
  
  const now = new Date();
  const diffMs = dateObj.getTime() - now.getTime();
  const isPast = diffMs < 0;
  const absDiffMs = Math.abs(diffMs);
  
  const diffSeconds = Math.floor(absDiffMs / 1000);
  const diffMinutes = Math.floor(diffSeconds / 60);
  const diffHours = Math.floor(diffMinutes / 60);
  const diffDays = Math.floor(diffHours / 24);
  const diffWeeks = Math.floor(diffDays / 7);
  const diffMonths = Math.floor(diffDays / 30);
  const diffYears = Math.floor(diffDays / 365);
  
  // Very soon (less than 1 minute)
  if (diffSeconds < 60) {
    return isPast ? t("time.justNow") : t("time.soon");
  }
  
  // Past times
  if (isPast) {
    if (diffMinutes < 60) {
      return t("time.minutesAgo", { count: diffMinutes });
    } else if (diffHours < 24) {
      return t("time.hoursAgo", { count: diffHours });
    } else if (diffDays === 1) {
      return t("time.yesterday");
    } else if (diffDays < 7) {
      return t("time.daysAgo", { count: diffDays });
    } else if (diffWeeks < 4) {
      return diffWeeks === 1 ? t("time.weeksAgo", { count: 1 }) : t("time.weeksAgo", { count: diffWeeks });
    } else if (diffMonths < 12) {
      return diffMonths === 1 ? t("time.monthsAgo", { count: 1 }) : t("time.monthsAgo", { count: diffMonths });
    } else {
      return diffYears === 1 ? t("time.yearsAgo", { count: 1 }) : t("time.yearsAgo", { count: diffYears });
    }
  }
  
  // Future times
  if (diffMinutes < 60) {
    return t("time.inMinutes", { count: diffMinutes });
  } else if (diffHours < 24) {
    return t("time.inHours", { count: diffHours });
  } else if (diffDays === 1) {
    return t("time.tomorrow");
  } else if (diffDays < 7) {
    return t("time.inDays", { count: diffDays });
  } else if (diffWeeks < 4) {
    return diffWeeks === 1 ? t("time.inWeeks", { count: 1 }) : t("time.inWeeks", { count: diffWeeks });
  } else if (diffMonths < 12) {
    return diffMonths === 1 ? t("time.inMonths", { count: 1 }) : t("time.inMonths", { count: diffMonths });
  } else {
    return diffYears === 1 ? t("time.inYears", { count: 1 }) : t("time.inYears", { count: diffYears });
  }
}
