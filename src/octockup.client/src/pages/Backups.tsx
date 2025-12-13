import {
  Box,
  Card,
  Stack,
  Button,
  Typography,
  CardContent,
  CircularProgress,
} from "@mui/material";
import { AddCircleOutline } from "@mui/icons-material";
import { useEffect, useState, useCallback } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { BackupItem, ScheduleReport } from "../types/api";
import { useBackupsApi } from "../api/backupsApi";
import { useSchedulesApi } from "../api/schedulesApi";
import { useSignalR } from "../hooks/useSignalR";
import { BackupStatus } from "../types/api";
import { getBackupOverallStatus } from "../utils/backupUtils";
import { EditIgnoredPathsDialog } from "../components/EditIgnoredPathsDialog";
import { BackupCard } from "../components/backups/BackupCard";

interface State {
  loading: boolean;
  deletingId: string | null;
  runningId: string | null;
  cancelingId: string | null;
}

export default function BackupsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const backupsApi = useBackupsApi();
  const schedulesApi = useSchedulesApi();
  const { connection, isConnected } = useSignalR("/api/v1/event-hub");
  const [state, setState] = useState<State>({
    loading: true,
    deletingId: null,
    runningId: null,
    cancelingId: null,
  });
  const [backups, setBackups] = useState<BackupItem[]>([]);
  const [scheduleReports, setScheduleReports] = useState<
    Record<string, ScheduleReport>
  >({});
  const [scheduleToBackupMap, setScheduleToBackupMap] = useState<
    Record<string, string>
  >({});
  const [editingIgnoredPathsId, setEditingIgnoredPathsId] = useState<
    string | null
  >(null);
  const [savingIgnoredPathsId, setSavingIgnoredPathsId] = useState<
    string | null
  >(null);

  const reloadBackups = useCallback(async () => {
    try {
      const backupList = await backupsApi.list();
      setBackups(backupList);

      const mapping: Record<string, string> = {};
      backupList.forEach((backup) => {
        backup.schedules?.forEach((schedule) => {
          mapping[schedule.id] = schedule.backupId;
        });
      });
      setScheduleToBackupMap(mapping);
    } catch {
      // Silent fail
    }
  }, [backupsApi]);

  useEffect(() => {
    let active = true;

    // Load backups - schedules already included in backup.schedules
    backupsApi
      .list()
      .then((backupList) => {
        if (!active) return;
        setBackups(backupList);

        // Create mapping scheduleId -> backupId from embedded schedules
        const mapping: Record<string, string> = {};
        backupList.forEach((backup) => {
          backup.schedules?.forEach((schedule) => {
            mapping[schedule.id] = schedule.backupId;
          });
        });
        setScheduleToBackupMap(mapping);

        setState((s) => ({ ...s, loading: false }));
      })
      .catch(() => {
        if (!active) return;
        // Silently fail for background requests - just stop loading
        setState((s) => ({
          ...s,
          loading: false,
        }));
      });

    return () => {
      active = false;
    };
  }, [backupsApi]);

  // WebSocket listener for schedule reports
  useEffect(() => {
    if (!connection || !isConnected) return;

    const handler = (report: ScheduleReport) => {
      setScheduleReports((prev) => {
        const prevReport = prev[report.scheduleId];

        // If status changed from Running to Completed/Failed, reload backups
        if (
          prevReport?.status === BackupStatus.Running &&
          report.status !== BackupStatus.Running
        ) {
          // Reload backups after a short delay to ensure backend updated
          setTimeout(() => {
            reloadBackups();
          }, 500);
        }

        return {
          ...prev,
          [report.scheduleId]: report,
        };
      });

      // Update mapping if new schedule appeared
      setScheduleToBackupMap((prev) => {
        if (!prev[report.scheduleId]) {
          return {
            ...prev,
            [report.scheduleId]: report.backupId,
          };
        }
        return prev;
      });
    };

    connection.on("ScheduleReport", handler);

    return () => {
      connection.off("ScheduleReport", handler);
    };
  }, [connection, isConnected, reloadBackups]);

  const handleRename = async (backupId: string, newTag: string) => {
    await backupsApi.rename(backupId, newTag);
    setBackups((prev) =>
      prev.map((b) => (b.id === backupId ? { ...b, tag: newTag } : b)),
    );
  };

  const handleSaveIgnoredPaths = async (backupId: string, paths: string[]) => {
    setSavingIgnoredPathsId(backupId);
    try {
      await backupsApi.updateIgnoredPaths(backupId, paths);
      setBackups((prev) =>
        prev.map((b) =>
          b.id === backupId ? { ...b, ignoredPaths: paths } : b,
        ),
      );
    } finally {
      setSavingIgnoredPathsId(null);
    }
  };

  const handleRunOnce = async (backupId: string) => {
    setState((s) => ({ ...s, runningId: backupId }));
    try {
      await schedulesApi.create({
        backupId,
        startAt: new Date().toISOString(),
      });

      // Reload backups to get the actual schedule from server
      await reloadBackups();
    } finally {
      setState((s) => ({ ...s, runningId: null }));
    }
  };

  const handleCancel = async (backupId: string, scheduleId: string) => {
    setState((s) => ({ ...s, cancelingId: backupId }));
    try {
      // Remove the schedule report immediately to prevent UI showing "running"
      setScheduleReports((prev) => {
        const newReports = { ...prev };
        delete newReports[scheduleId];
        return newReports;
      });

      // Reload backups immediately to get fresh status
      await reloadBackups();
    } finally {
      setState((s) => ({ ...s, cancelingId: null }));
    }
  };

  const handleDelete = async (backupId: string) => {
    setState((s) => ({ ...s, deletingId: backupId }));
    await backupsApi.delete(backupId);
    setBackups((prev) => prev.filter((x) => x.id !== backupId));
    setState((s) => ({ ...s, deletingId: null }));
  };

  if (state.loading && backups.length === 0) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      <Box display="flex" alignItems="center" justifyContent="space-between">
        <Typography variant="h5">{t("backups.title")}</Typography>
        <Button
          variant="contained"
          startIcon={<AddCircleOutline />}
          onClick={() => navigate("/backups/new")}
        >
          {t("backups.newBackup")}
        </Button>
      </Box>
      {backups.length === 0 ? (
        <Card>
          <CardContent>
            <Typography color="text.secondary">
              {t("backups.noBackups")}
            </Typography>
          </CardContent>
        </Card>
      ) : (
        <Stack spacing={1}>
          {backups
            .slice()
            .sort((a, b) => {
              const statusA = getBackupOverallStatus(
                a,
                scheduleToBackupMap,
                scheduleReports,
              );
              const statusB = getBackupOverallStatus(
                b,
                scheduleToBackupMap,
                scheduleReports,
              );

              const priorityMap: Record<string, number> = {
                running: 1,
                failed: 2,
                warning: 3,
                scheduled: 4,
                success: 5,
                idle: 6,
              };

              const priorityA = priorityMap[statusA] || 999;
              const priorityB = priorityMap[statusB] || 999;

              if (priorityA !== priorityB) {
                return priorityA - priorityB;
              }

              return (
                new Date(b.createdAt || 0).getTime() -
                new Date(a.createdAt || 0).getTime()
              );
            })
            .map((b) => (
              <BackupCard
                key={b.id}
                backup={b}
                scheduleToBackupMap={scheduleToBackupMap}
                scheduleReports={scheduleReports}
                runningId={state.runningId}
                cancelingId={state.cancelingId}
                deletingId={state.deletingId}
                savingIgnoredPathsId={savingIgnoredPathsId}
                onRename={handleRename}
                onEditIgnoredPaths={setEditingIgnoredPathsId}
                onRunOnce={handleRunOnce}
                onCancel={handleCancel}
                onDelete={handleDelete}
              />
            ))}
        </Stack>
      )}
      {editingIgnoredPathsId &&
        (() => {
          const backup = backups.find((b) => b.id === editingIgnoredPathsId);
          if (!backup) return null;
          return (
            <EditIgnoredPathsDialog
              open={true}
              backupModuleId={backup.source.backupModuleId}
              initialPaths={backup.ignoredPaths}
              onClose={() => setEditingIgnoredPathsId(null)}
              onSave={(paths) =>
                handleSaveIgnoredPaths(editingIgnoredPathsId, paths)
              }
              loading={savingIgnoredPathsId === editingIgnoredPathsId}
            />
          );
        })()}
    </Stack>
  );
}
