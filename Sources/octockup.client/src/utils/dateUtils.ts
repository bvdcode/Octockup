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
