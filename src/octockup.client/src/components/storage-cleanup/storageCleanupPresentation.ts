import type { ChipProps } from "@mui/material";
import type { TFunction } from "i18next";
import { StorageCleanupStatus } from "../../types/storageCleanup";

export function getStorageCleanupStatusColor(
  status: StorageCleanupStatus,
): ChipProps["color"] {
  switch (status) {
    case StorageCleanupStatus.Idle:
      return "default";
    case StorageCleanupStatus.Running:
      return "info";
    case StorageCleanupStatus.Completed:
      return "success";
    case StorageCleanupStatus.Failed:
      return "error";
  }
}

export function getStorageCleanupStatusKey(
  status: StorageCleanupStatus,
): string {
  switch (status) {
    case StorageCleanupStatus.Idle:
      return "storageCleanup.status.idle";
    case StorageCleanupStatus.Running:
      return "storageCleanup.status.running";
    case StorageCleanupStatus.Completed:
      return "storageCleanup.status.completed";
    case StorageCleanupStatus.Failed:
      return "storageCleanup.status.failed";
  }
}

export function getRunDurationSeconds(
  startedAt: string,
  completedAt?: string | null,
): number {
  const start = new Date(startedAt).getTime();
  const end = completedAt ? new Date(completedAt).getTime() : Date.now();
  return Math.max(0, (end - start) / 1000);
}

export function formatStorageCleanupDuration(
  totalSeconds: number,
  t: TFunction,
): string {
  const seconds = Math.round(totalSeconds);
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const remainingSeconds = seconds % 60;

  if (hours > 0) {
    return t("storageCleanup.duration.hoursMinutes", { hours, minutes });
  }
  if (minutes > 0) {
    return t("storageCleanup.duration.minutesSeconds", {
      minutes,
      seconds: remainingSeconds,
    });
  }
  return t("storageCleanup.duration.seconds", { seconds: remainingSeconds });
}
