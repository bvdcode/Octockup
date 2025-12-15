import {
  Box,
  Card,
  Stack,
  Button,
  Typography,
  CardContent,
  CircularProgress,
  Divider,
  Select,
  MenuItem,
  FormControl,
} from "@mui/material";
import { AddCircleOutline } from "@mui/icons-material";
import { useEffect, useState, useCallback, useMemo } from "react";
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
import { formatSize } from "../utils/formatUtils";

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
  const [scheduleReports] = useState<Map<string, ScheduleReport>>(new Map());
  const [, setReportsVersion] = useState(0);
  const [scheduleToBackupMap, setScheduleToBackupMap] = useState<
    Record<string, string>
  >({});
  const [editingIgnoredPathsId, setEditingIgnoredPathsId] = useState<
    string | null
  >(null);
  const [savingIgnoredPathsId, setSavingIgnoredPathsId] = useState<
    string | null
  >(null);
  const [selectedStorageId, setSelectedStorageId] = useState<string | null>(
    null,
  );

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
      const backupId = report.backupId;

      // Mutate the Map directly instead of cloning it
      if (report.status === BackupStatus.Running) {
        scheduleReports.set(backupId, report);
      } else {
        scheduleReports.delete(backupId);
      }

      // Force re-render by incrementing version
      setReportsVersion((v) => v + 1);

      // Reload backups whenever status changes to get fresh data
      if (report.status !== BackupStatus.Running) {
        setTimeout(() => {
          reloadBackups();
        }, 500);
      }

      // Update mapping
      setScheduleToBackupMap((prev) => ({
        ...prev,
        [report.scheduleId]: backupId,
      }));
    };

    connection.on("ScheduleReport", handler);

    return () => {
      connection.off("ScheduleReport", handler);
    };
  }, [connection, isConnected, reloadBackups, scheduleReports]);

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

  const handleCancel = async (scheduleId: string) => {
    const backupIdFromMap = scheduleToBackupMap[scheduleId];
    const backupEntry =
      backups.find((b) => b.schedules?.some((s) => s.id === scheduleId)) ||
      null;
    const backupId = backupIdFromMap || backupEntry?.id || null;

    if (backupId) {
      setState((s) => ({ ...s, cancelingId: backupId }));
    }

    try {
      await schedulesApi.cancel(scheduleId);
    } finally {
      if (backupId) {
        setState((s) => ({ ...s, cancelingId: null }));
      }
    }
  };

  const handleDelete = async (backupId: string) => {
    setState((s) => ({ ...s, deletingId: backupId }));
    await backupsApi.delete(backupId);
    setBackups((prev) => prev.filter((x) => x.id !== backupId));
    setState((s) => ({ ...s, deletingId: null }));
  };

  const totalStats = useMemo(() => {
    let totalFiles = 0;
    let totalSize = 0;

    const filteredBackups = selectedStorageId
      ? backups.filter((b) => b.storageId === selectedStorageId)
      : backups;

    filteredBackups.forEach((backup) => {
      const lastSnapshot = backup.snapshots
        ?.filter((s) => s.completedAt)
        .sort(
          (a, b) =>
            new Date(b.completedAt!).getTime() -
            new Date(a.completedAt!).getTime(),
        )[0];

      if (lastSnapshot) {
        totalFiles += lastSnapshot.filesCount;
        totalSize += lastSnapshot.totalSize;
      }
    });

    return { totalFiles, totalSize };
  }, [backups, selectedStorageId]);

  const uniqueStorages = useMemo(() => {
    const storageMap = new Map<string, { id: string; tag: string }>();
    backups.forEach((backup) => {
      if (!storageMap.has(backup.storageId)) {
        storageMap.set(backup.storageId, {
          id: backup.storageId,
          tag: backup.storage.tag,
        });
      }
    });
    return Array.from(storageMap.values()).sort((a, b) =>
      a.tag.localeCompare(b.tag),
    );
  }, [backups]);

  const filteredBackups = useMemo(() => {
    return selectedStorageId
      ? backups.filter((b) => b.storageId === selectedStorageId)
      : backups;
  }, [backups, selectedStorageId]);

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
        <Box display="flex" alignItems="center" gap={2}>
          <Typography variant="h5">{t("backups.title")}</Typography>
          <Divider orientation="vertical" flexItem />
          <Typography variant="body2" color="text.secondary">
            {t("backups.totalFiles", {
              count: totalStats.totalFiles,
            })}
          </Typography>
          <Divider orientation="vertical" flexItem />
          <Typography variant="body2" color="text.secondary">
            {t("backups.totalSize", {
              size: formatSize(totalStats.totalSize),
            })}
          </Typography>
        </Box>
        <Box display="flex" alignItems="center" gap={2}>
          <FormControl size="small" sx={{ minWidth: 150 }}>
            <Select
              value={selectedStorageId || "all"}
              onChange={(e) =>
                setSelectedStorageId(
                  e.target.value === "all" ? null : e.target.value,
                )
              }
              displayEmpty
            >
              <MenuItem value="all">{t("backups.allStorages")}</MenuItem>
              {uniqueStorages.map((storage) => (
                <MenuItem key={storage.id} value={storage.id}>
                  {storage.tag}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <Button
            variant="contained"
            startIcon={<AddCircleOutline />}
            onClick={() => navigate("/backups/new")}
          >
            {t("backups.newBackup")}
          </Button>
        </Box>
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
          {filteredBackups
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
                created: 5,
                success: 6,
                idle: 7,
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
