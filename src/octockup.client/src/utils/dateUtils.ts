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
 * Formats a date as a relative time string for both past and future
 * Past: "2 minutes ago", "yesterday", "last week"
 * Future: "in 2 minutes", "tomorrow", "in 1 week"
 */
export function formatRelativeTime(
  date: Date | string | null | undefined, 
  t: (key: string, options?: any) => string,
  mode: "past" | "future" = "past"
): string {
  if (!date) return t("common.never");
  
  const dateObj = typeof date === "string" ? parseUtcDate(date) : date;
  if (!dateObj) return t("common.never");
  
  const now = new Date();
  const diffMs = mode === "past" 
    ? now.getTime() - dateObj.getTime()
    : dateObj.getTime() - now.getTime();
  
  const diffSeconds = Math.floor(diffMs / 1000);
  const diffMinutes = Math.floor(diffSeconds / 60);
  const diffHours = Math.floor(diffMinutes / 60);
  const diffDays = Math.floor(diffHours / 24);
  const diffWeeks = Math.floor(diffDays / 7);
  const diffMonths = Math.floor(diffDays / 30);
  const diffYears = Math.floor(diffDays / 365);
  
  if (mode === "past") {
    if (diffSeconds < 60) {
      return t("time.justNow");
    } else if (diffMinutes < 60) {
      return t("time.minutesAgo", { count: diffMinutes });
    } else if (diffHours < 24) {
      return t("time.hoursAgo", { count: diffHours });
    } else if (diffDays === 1) {
      return t("time.yesterday");
    } else if (diffDays < 7) {
      return t("time.daysAgo", { count: diffDays });
    } else if (diffWeeks < 4) {
      return t("time.weeksAgo", { count: diffWeeks });
    } else if (diffMonths < 12) {
      return t("time.monthsAgo", { count: diffMonths });
    } else {
      return t("time.yearsAgo", { count: diffYears });
    }
  } else {
    // Future mode
    if (diffMinutes < 1) {
      return t("time.soon");
    } else if (diffMinutes < 60) {
      return t("time.inMinutes", { count: diffMinutes });
    } else if (diffHours < 24) {
      return t("time.inHours", { count: diffHours });
    } else if (diffDays === 1) {
      return t("time.tomorrow");
    } else if (diffDays < 7) {
      return t("time.inDays", { count: diffDays });
    } else if (diffWeeks < 4) {
      return t("time.inWeeks", { count: diffWeeks });
    } else if (diffMonths < 12) {
      return t("time.inMonths", { count: diffMonths });
    } else {
      return t("time.inYears", { count: diffYears });
    }
  }
}
