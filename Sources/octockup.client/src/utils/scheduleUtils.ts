import { BackupStatus } from "../types/api";
import type { ScheduleItem } from "../types/api";
import { parseUtcDate } from "./dateUtils";

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
  const parts = String(interval).split(":");
  return parts.length >= 2 ? parseInt(parts[0]) * 60 + parseInt(parts[1]) : 0;
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

  // Calculate based on last finished time or start time
  if (
    item.finishedAt &&
    (item.status === BackupStatus.Completed ||
      item.status === BackupStatus.Failed)
  ) {
    const finishedAt = parseUtcDate(item.finishedAt);
    if (finishedAt) {
      return new Date(finishedAt.getTime() + intervalMinutes * 60000);
    }
  }

  return new Date(startAt.getTime() + intervalMinutes * 60000);
}

export function formatNextRun(
  item: ScheduleItem,
  t: (key: string, options?: Record<string, unknown>) => string,
): string {
  const now = new Date();
  const nextRun = calculateNextRunTime(item);

  // No next run scheduled
  if (!nextRun) {
    if (item.status === BackupStatus.Running) {
      return t("schedules.nextRun.afterCurrent", {
        defaultValue: "After current run",
      });
    }
    if (!item.interval) {
      return t("schedules.nextRun.never", { defaultValue: "Never" });
    }
    return t("schedules.nextRun.unknown", { defaultValue: "Unknown" });
  }

  const diff = nextRun.getTime() - now.getTime();

  // Already passed or very soon
  if (diff < 60000) {
    return t("schedules.nextRun.soon", { defaultValue: "Soon" });
  }

  // Format based on time difference
  const minutes = Math.floor(diff / 60000);
  const hours = Math.floor(diff / 3600000);
  const days = Math.floor(diff / 86400000);
  const weeks = Math.floor(diff / 604800000);
  const months = Math.floor(diff / 2592000000);

  if (diff < 3600000) {
    return t("schedules.nextRun.inMinutes", {
      defaultValue: "In {{count}} minutes",
      count: minutes,
    });
  }

  if (diff < 86400000) {
    return t("schedules.nextRun.inHours", {
      defaultValue: "In {{count}} hours",
      count: hours,
    });
  }

  if (diff < 172800000) {
    return t("schedules.nextRun.tomorrow", { defaultValue: "Tomorrow" });
  }

  if (diff < 604800000) {
    return t("schedules.nextRun.inDays", {
      defaultValue: "In {{count}} days",
      count: days,
    });
  }

  if (diff < 2592000000) {
    return t("schedules.nextRun.inWeeks", {
      defaultValue: "In {{count}} weeks",
      count: weeks,
    });
  }

  return t("schedules.nextRun.inMonths", {
    defaultValue: "In {{count}} months",
    count: months,
  });
}
