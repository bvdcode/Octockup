/**
 * Parses a datetime string from the backend and treats values without an
 * explicit timezone as UTC.
 */
export function parseUtcDate(
  dateString: string | null | undefined,
): Date | null {
  if (!dateString) {
    return null;
  }

  if (
    dateString.endsWith("Z") ||
    dateString.includes("+") ||
    /[+-]\d{2}:\d{2}$/.test(dateString)
  ) {
    return new Date(dateString);
  }

  return new Date(dateString + "Z");
}

type Translator = (key: string, options?: { count: number }) => string;

interface RelativeTimeParts {
  seconds: number;
  minutes: number;
  hours: number;
  days: number;
  weeks: number;
  months: number;
  years: number;
}

export function formatRelativeTime(
  date: Date | string | null | undefined,
  t: Translator,
  mode: "past" | "future" = "past",
): string {
  if (!date) {
    return t("common.never");
  }

  const dateObj = typeof date === "string" ? parseUtcDate(date) : date;
  if (!dateObj) {
    return t("common.never");
  }

  const now = new Date();
  const differenceMilliseconds =
    mode === "past"
      ? now.getTime() - dateObj.getTime()
      : dateObj.getTime() - now.getTime();
  const parts = getRelativeTimeParts(differenceMilliseconds);
  return mode === "past" ? formatPastTime(parts, t) : formatFutureTime(parts, t);
}

function getRelativeTimeParts(differenceMilliseconds: number): RelativeTimeParts {
  const seconds = Math.floor(differenceMilliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  const hours = Math.floor(minutes / 60);
  const days = Math.floor(hours / 24);
  return {
    seconds,
    minutes,
    hours,
    days,
    weeks: Math.floor(days / 7),
    months: Math.floor(days / 30),
    years: Math.floor(days / 365),
  };
}

function formatPastTime(parts: RelativeTimeParts, t: Translator): string {
  if (parts.seconds < 60) {
    return t("time.justNow");
  }
  if (parts.minutes < 60) {
    return t("time.minutesAgo", { count: parts.minutes });
  }
  if (parts.hours < 24) {
    return t("time.hoursAgo", { count: parts.hours });
  }
  if (parts.days === 1) {
    return t("time.yesterday");
  }
  if (parts.days < 7) {
    return t("time.daysAgo", { count: parts.days });
  }
  if (parts.weeks < 4) {
    return t("time.weeksAgo", { count: parts.weeks });
  }
  if (parts.months < 12) {
    return t("time.monthsAgo", { count: parts.months });
  }
  return t("time.yearsAgo", { count: parts.years });
}

function formatFutureTime(parts: RelativeTimeParts, t: Translator): string {
  if (parts.minutes < 1) {
    return t("time.soon");
  }
  if (parts.minutes < 60) {
    return t("time.inMinutes", { count: parts.minutes });
  }
  if (parts.hours < 24) {
    return t("time.inHours", { count: parts.hours });
  }
  if (parts.days === 1) {
    return t("time.tomorrow");
  }
  if (parts.days < 7) {
    return t("time.inDays", { count: parts.days });
  }
  if (parts.weeks < 4) {
    return t("time.inWeeks", { count: parts.weeks });
  }
  if (parts.months < 12) {
    return t("time.inMonths", { count: parts.months });
  }
  return t("time.inYears", { count: parts.years });
}
