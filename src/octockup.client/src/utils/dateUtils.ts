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
 * Formats a date as a relative time string (e.g., "2 minutes ago", "yesterday", "last week")
 */
export function formatRelativeTime(date: Date | string | null | undefined, t: (key: string, options?: any) => string): string {
  if (!date) return t("common.never");
  
  const dateObj = typeof date === "string" ? parseUtcDate(date) : date;
  if (!dateObj) return t("common.never");
  
  const now = new Date();
  const diffMs = now.getTime() - dateObj.getTime();
  const diffSeconds = Math.floor(diffMs / 1000);
  const diffMinutes = Math.floor(diffSeconds / 60);
  const diffHours = Math.floor(diffMinutes / 60);
  const diffDays = Math.floor(diffHours / 24);
  
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
  } else if (diffDays < 30) {
    const weeks = Math.floor(diffDays / 7);
    return t("time.weeksAgo", { count: weeks });
  } else if (diffDays < 365) {
    const months = Math.floor(diffDays / 30);
    return t("time.monthsAgo", { count: months });
  } else {
    const years = Math.floor(diffDays / 365);
    return t("time.yearsAgo", { count: years });
  }
}
