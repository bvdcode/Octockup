import { BackupStatus } from "../types/api";
import type { ScheduleReport, BackupItem } from "../types/api";

export type BackupOverallStatus =
  | "running"
  | "failed"
  | "warning"
  | "scheduled"
  | "success"
  | "created"
  | "idle";

/**
 * Determines the overall status of a backup based on its schedules and snapshots
 * Priority (from highest to lowest):
 * 1. Running - backup is currently executing (blue)
 * 2. Scheduled - has pending schedules (yellow)
 * 3. Failed - has failed schedules with errors (red)
 * 4. Warning - has successful snapshot, but schedule after it failed (orange)
 * 5. Success - has successful snapshots, no errors after them (green)
 * 6. Created - new backup, no snapshots yet (gray)
 * 7. Idle - has snapshots, no active schedules (gray)
 */
export function getBackupOverallStatus(
  backup: BackupItem,
  _scheduleToBackupMap: Record<string, string>,
  scheduleReports: Map<string, ScheduleReport>,
): BackupOverallStatus {
  const hasRunningInSchedules =
    backup.activeSchedule?.status === BackupStatus.Running;

  const reportForBackup = scheduleReports.get(backup.id);
  const hasRunningInReports =
    reportForBackup?.status === BackupStatus.Running;

  if (hasRunningInSchedules || hasRunningInReports) {
    return "running";
  }

  // Priority 2: Scheduled - check for pending schedules FIRST (before checking snapshots)
  const hasPendingSchedules =
    backup.activeSchedule?.status === BackupStatus.Created;

  if (hasPendingSchedules) {
    return "scheduled";
  }

  const latestSuccessfulSnapshot = backup.latestSnapshot?.completedAt
    ? backup.latestSnapshot
    : null;

  // Priority 3: No snapshots yet - check if there are any failed schedules
  if (!latestSuccessfulSnapshot) {
    // If there are failed schedules with errors, show as failed (red)
    const hasFailedSchedules =
      backup.latestFinishedSchedule?.status === BackupStatus.Failed &&
      !!backup.latestFinishedSchedule.errorMessage;

    if (hasFailedSchedules) {
      return "failed";
    }

    // Otherwise, it's just a new backup - show as created (gray)
    return "created";
  }

  const latestFinishedSchedule = backup.latestFinishedSchedule;
  const hasFailedScheduleAfterSnapshot =
    latestFinishedSchedule?.status === BackupStatus.Failed &&
    !!latestFinishedSchedule.errorMessage &&
    !!latestFinishedSchedule.finishedAt &&
    new Date(latestFinishedSchedule.finishedAt).getTime() >
      new Date(latestSuccessfulSnapshot.completedAt!).getTime();

  if (hasFailedScheduleAfterSnapshot) {
    return "warning";
  }

  // Priority 5: Success - has successful snapshot(s) and no errors after
  if (latestSuccessfulSnapshot) {
    return "success";
  }

  // Priority 6: Idle - has snapshots but no active schedules
  return "idle";
}
