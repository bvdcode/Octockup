import { BackupStatus } from "../types/api";
import type { ScheduleItem } from "../types/api";
import type { TFunction } from "i18next";
import { parseUtcDate, formatRelativeTime } from "./dateUtils";

export function statusColor(
  status: BackupStatus,
): "default" | "success" | "error" | "warning" | "info" {
  switch (status) {
    case BackupStatus.Completed:
      return "success";
    case BackupStatus.Running:
      return "info";
    case BackupStatus.Failed:
      return "error";
    case BackupStatus.Created:
    default:
      return "default";
  }
}

export function parseInterval(interval: string | null): number {
  if (!interval) return 0;
  const intervalStr = String(interval);
  
  // Handle TimeSpan format: "d.HH:mm:ss" or "HH:mm:ss"
  if (intervalStr.includes(".")) {
    const [dayPart, timePart] = intervalStr.split(".");
    const days = parseInt(dayPart);
    const timeParts = timePart.split(":");
    const hours = parseInt(timeParts[0]) || 0;
    const minutes = parseInt(timeParts[1]) || 0;
    return days * 24 * 60 + hours * 60 + minutes;
  }
  
  // Handle time format: "HH:mm:ss"
  const parts = intervalStr.split(":");
  return parts.length >= 2 ? parseInt(parts[0]) * 60 + parseInt(parts[1]) : 0;
}

export function formatInterval(
  interval: string | null,
  t: TFunction,
): string {
  if (!interval) return "";
  const minutes = parseInterval(interval);
  
  // Convert to different time units
  const days = Math.floor(minutes / (24 * 60));
  const hours = Math.floor((minutes % (24 * 60)) / 60);
  const mins = minutes % 60;
  
  // Return human-readable format
  if (days > 0) {
    if (days === 1) return t("schedules.interval1d");
    if (days === 7) return t("schedules.interval1w");
    if (days === 30) return t("schedules.interval1m");
    return t("schedules.intervalDays", { count: days });
  }
  
  if (hours > 0) {
    if (hours === 1) return t("schedules.interval1h");
    return t("schedules.intervalHours", { count: hours });
  }
  
  return t("schedules.intervalMinutes", { count: mins });
}

export function calculateNextRunTime(
  item: ScheduleItem,
): Date | null {
  const now = new Date();
  const startAt = parseUtcDate(item.startAt);
  if (!startAt) return null;

  // One-time schedule
  if (!item.interval) {
    if (
      item.status === BackupStatus.Completed ||
      item.status === BackupStatus.Failed
    ) {
      return null;
    }
    return startAt > now ? startAt : now;
  }

  const intervalMinutes = parseInterval(item.interval);
  if (intervalMinutes === 0) return null;

  // Running schedule
  if (item.status === BackupStatus.Running) {
    return null; // Cannot determine until current run finishes
  }

  // Keep behavior aligned with backend ScheduleHelpers.CalculateNextRun.
  // For periodic schedules: if never finished, next run is StartAt (not StartAt + interval).
  if (startAt > now) {
    return startAt;
  }

  if (item.finishedAt) {
    const finishedAt = parseUtcDate(item.finishedAt);
    if (finishedAt) {
      return new Date(finishedAt.getTime() + intervalMinutes * 60000);
    }
  }

  return startAt;
}

export function formatNextRun(
  item: ScheduleItem,
  t: TFunction,
): string {
  const nextRun = calculateNextRunTime(item);

  // No next run scheduled
  if (!nextRun) {
    if (item.status === BackupStatus.Running) {
      return t("schedules.nextRun.afterCurrent");
    }
    if (!item.interval) {
      return t("schedules.nextRun.never");
    }
    return t("schedules.nextRun.unknown");
  }

  // Use unified relative time formatter for future dates
  return formatRelativeTime(nextRun, t, "future");
}
