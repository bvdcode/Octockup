import type { HubConnection } from "@microsoft/signalr";
import { useEffect, useState } from "react";
import { BackupStatus, type BackupItem, type ScheduleReport } from "../types/api";

const TERMINAL_REFRESH_DELAY_MS = 500;

export function useBackupScheduleReports(
  connection: HubConnection | null,
  isConnected: boolean,
  updateBackups: (updater: (current: BackupItem[]) => BackupItem[]) => void,
  reloadBackups: () => Promise<void>,
): Map<string, ScheduleReport> {
  const [reports, setReports] = useState<Map<string, ScheduleReport>>(
    () => new Map(),
  );

  useEffect(() => {
    if (!connection || !isConnected) {
      return;
    }

    const refreshTimers = new Set<number>();
    const handler = (report: ScheduleReport) => {
      setReports((current) => {
        const next = new Map(current);
        if (report.status === BackupStatus.Running) {
          next.set(report.backupId, report);
        } else {
          next.delete(report.backupId);
        }
        return next;
      });

      updateBackups((current) =>
        current.map((backup) =>
          backup.id === report.backupId
            ? {
                ...backup,
                schedules: backup.schedules.map((schedule) =>
                  schedule.id === report.scheduleId
                    ? {
                        ...schedule,
                        status: report.status,
                        errorMessage:
                          report.status === BackupStatus.Failed
                            ? report.message
                            : null,
                        finishedAt:
                          report.status === BackupStatus.Running
                            ? null
                            : report.timestamp,
                      }
                    : schedule,
                ),
              }
            : backup,
        ),
      );

      if (report.status !== BackupStatus.Running) {
        const timer = window.setTimeout(() => {
          refreshTimers.delete(timer);
          void reloadBackups();
        }, TERMINAL_REFRESH_DELAY_MS);
        refreshTimers.add(timer);
      }
    };

    connection.on("ScheduleReport", handler);
    return () => {
      connection.off("ScheduleReport", handler);
      refreshTimers.forEach((timer) => window.clearTimeout(timer));
    };
  }, [connection, isConnected, reloadBackups, updateBackups]);

  return reports;
}
