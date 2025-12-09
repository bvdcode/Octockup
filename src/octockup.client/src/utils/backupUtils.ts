import { BackupStatus } from "../types/api";
import type { ScheduleReport, BackupItem } from "../types/api";

export type BackupOverallStatus = "running" | "scheduled" | "failed" | "success" | "idle";

/**
 * Determines the overall status of a backup based on its schedules and snapshots
 * Priority: 
 * 1. Running - backup is currently executing
 * 2. Scheduled - backup has pending schedules
 * 3. Failed - no successful snapshots OR last snapshot failed
 * 4. Success - has at least one completed snapshot
 * 5. Idle - no activity
 */
export function getBackupOverallStatus(
  backup: BackupItem,
  scheduleToBackupMap: Record<string, string>,
  scheduleReports: Record<string, ScheduleReport>
): BackupOverallStatus {
  const backupId = backup.id;
  // Find all schedules for this backup
  const backupSchedules = Object.entries(scheduleToBackupMap)
    .filter(([_, bId]) => bId === backupId)
    .map(([scheduleId]) => scheduleReports[scheduleId])
    .filter(Boolean);

  // Check for running schedules (highest priority)
  const hasRunning = backupSchedules.some(
    (report) => report.status === BackupStatus.Running
  );
  if (hasRunning) {
    return "running";
  }

  // Check for created/scheduled schedules
  const hasScheduled = backupSchedules.some(
    (report) => report.status === BackupStatus.Created
  );
  if (hasScheduled) {
    return "scheduled";
  }

  // Check snapshots
  const completedSnapshots = backup.snapshots?.filter(
    (snapshot) => snapshot.completedAt
  ) || [];

  const hasSuccessfulSnapshot = completedSnapshots.some(
    (snapshot) => !snapshot.errorMessage
  );

  const lastSnapshot = completedSnapshots.sort(
    (a, b) => new Date(b.completedAt!).getTime() - new Date(a.completedAt!).getTime()
  )[0];

  // If no successful snapshots exist, it's failed
  if (!hasSuccessfulSnapshot && completedSnapshots.length > 0) {
    return "failed";
  }

  // Check for recent failed schedules (not currently running but failed)
  const hasRecentFailed = backupSchedules.some(
    (report) => report.status === BackupStatus.Failed
  );

  // If last snapshot failed but we have successful ones, show failed
  if (lastSnapshot?.errorMessage && hasRecentFailed) {
    return "failed";
  }

  // If we have at least one successful snapshot
  if (hasSuccessfulSnapshot) {
    return "success";
  }

  return "idle";
}
