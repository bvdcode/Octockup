import { BackupStatus } from "../types/api";
import type { ScheduleReport } from "../types/api";

export type BackupOverallStatus = "running" | "scheduled" | "failed" | "idle";

/**
 * Determines the overall status of a backup based on its schedules
 * Priority: Running > Failed > Scheduled > Idle
 */
export function getBackupOverallStatus(
  backupId: string,
  scheduleToBackupMap: Record<string, string>,
  scheduleReports: Record<string, ScheduleReport>
): BackupOverallStatus {
  // Find all schedules for this backup
  const backupSchedules = Object.entries(scheduleToBackupMap)
    .filter(([_, bId]) => bId === backupId)
    .map(([scheduleId]) => scheduleReports[scheduleId])
    .filter(Boolean);

  if (backupSchedules.length === 0) {
    return "idle";
  }

  // Check for running schedules
  const hasRunning = backupSchedules.some(
    (report) => report.status === BackupStatus.Running
  );
  if (hasRunning) {
    return "running";
  }

  // Check for failed schedules
  const hasFailed = backupSchedules.some(
    (report) => report.status === BackupStatus.Failed
  );
  if (hasFailed) {
    return "failed";
  }

  // Check for created/scheduled schedules
  const hasScheduled = backupSchedules.some(
    (report) => report.status === BackupStatus.Created
  );
  if (hasScheduled) {
    return "scheduled";
  }

  return "idle";
}
