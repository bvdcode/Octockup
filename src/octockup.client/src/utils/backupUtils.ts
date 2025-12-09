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
  scheduleToBackupMap: Record<string, string>,
  scheduleReports: Record<string, ScheduleReport>,
): BackupOverallStatus {
  const backupId = backup.id;

  // Find all schedules for this backup from reports (real-time)
  const backupSchedules = Object.entries(scheduleToBackupMap)
    .filter(([_, bId]) => bId === backupId)
    .map(([scheduleId]) => scheduleReports[scheduleId])
    .filter(Boolean);

  // Priority 1: Check for running schedules (highest priority)
  const hasRunning = backupSchedules.some(
    (report) => report.status === BackupStatus.Running,
  );
  if (hasRunning) {
    return "running";
  }

  // Analyze snapshots
  const completedSnapshots = (backup.snapshots || []).filter(
    (snapshot) => snapshot.completedAt && snapshot.filesCount > 0,
  );

  // Get the latest successful snapshot (has files)
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
      schedule.status === BackupStatus.Failed && schedule.errorMessage,
  );

  if (hasFailedScheduleAfterSnapshot) {
    return "warning";
  }

  // Priority 4: Check for created/scheduled schedules
  const hasScheduled = backupSchedules.some(
    (report) => report.status === BackupStatus.Created,
  );
  if (hasScheduled) {
    return "scheduled";
  }

  // Priority 5: Success - has successful snapshot(s) and no errors after
  if (latestSuccessfulSnapshot) {
    return "success";
  }

  // Priority 6: Idle - no activity
  return "idle";
}
