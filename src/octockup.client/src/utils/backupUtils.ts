import { BackupStatus } from "../types/api";
import type { ScheduleReport, BackupItem } from "../types/api";

export type BackupOverallStatus =
  | "running"
  | "failed"
  | "warning"
  | "scheduled"
  | "success"
  | "idle";

/**
 * Determines the overall status of a backup based on its schedules and snapshots
 * Priority (from highest to lowest):
 * 1. Running - backup is currently executing (blue)
 * 2. Failed - no successful snapshots at all (red, critical!)
 * 3. Warning - has successful snapshot, but schedule after it failed (orange)
 * 4. Scheduled - has pending schedules (yellow)
 * 5. Success - has successful snapshots, no errors after them (green)
 * 6. Idle - no activity (gray)
 */
export function getBackupOverallStatus(
  backup: BackupItem,
  _scheduleToBackupMap: Record<string, string>,
  scheduleReports: Map<string, ScheduleReport>,
): BackupOverallStatus {
  // Priority 1: Check for running schedules from backup.schedules OR scheduleReports
  const hasRunningInSchedules = (backup.schedules || []).some(
    (schedule) => schedule.status === BackupStatus.Running,
  );

  const reportForBackup = scheduleReports.get(backup.id);
  const hasRunningInReports =
    reportForBackup?.status === BackupStatus.Running;

  if (hasRunningInSchedules || hasRunningInReports) {
    return "running";
  }

  // Analyze snapshots - successful = has completedAt
  const completedSnapshots = (backup.snapshots || []).filter(
    (snapshot) => snapshot.completedAt,
  );

  // Get the latest successful snapshot
  const latestSuccessfulSnapshot = completedSnapshots.sort(
    (a, b) =>
      new Date(b.completedAt!).getTime() - new Date(a.completedAt!).getTime(),
  )[0];

  // Priority 2: Failed - no successful snapshots at all
  if (!latestSuccessfulSnapshot) {
    return "failed";
  }

  // Check schedules from backup.schedules (persistent data)
  const finishedSchedules = (backup.schedules || []).filter(
    (schedule) => schedule.finishedAt,
  );

  // Find schedules that finished after the latest successful snapshot
  const schedulesAfterSnapshot = finishedSchedules.filter((schedule) => {
    const scheduleFinishTime = new Date(schedule.finishedAt!).getTime();
    const snapshotTime = new Date(
      latestSuccessfulSnapshot.completedAt!,
    ).getTime();
    return scheduleFinishTime > snapshotTime;
  });

  // Priority 3: Warning - has successful snapshot, but schedule failed after it
  const hasFailedScheduleAfterSnapshot = schedulesAfterSnapshot.some(
    (schedule) =>
      (schedule.status === BackupStatus.Failed ||
        schedule.status === BackupStatus.Canceled) &&
      schedule.errorMessage,
  );

  if (hasFailedScheduleAfterSnapshot) {
    return "warning";
  }

  // Priority 4: Scheduled - backup has pending schedules
  const hasPendingSchedules = (backup.schedules || []).some(
    (schedule) =>
      schedule.status === BackupStatus.Created && !schedule.finishedAt,
  );

  if (hasPendingSchedules) {
    return "scheduled";
  }

  // Priority 5: Success - has successful snapshot(s) and no errors after
  if (latestSuccessfulSnapshot) {
    return "success";
  }

  // Priority 6: Idle - no activity
  return "idle";
}
