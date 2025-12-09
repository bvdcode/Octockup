import {
  Box,
  Card,
  Chip,
  Stack,
  Button,
  Divider,
  Tooltip,
  Typography,
  IconButton,
  CardContent,
  CircularProgress,
  LinearProgress,
} from "@mui/material";
import {
  PlayArrow,
  BackupTable,
  AccessTime,
  ArrowDownward,
  DeleteOutline,
  AddCircleOutline,
  FilterAlt,
} from "@mui/icons-material";
import { useEffect, useState } from "react";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { BackupItem, ScheduleReport } from "../types/api";
import { useBackupsApi } from "../api/backupsApi";
import { formatSize, formatSpeed, formatElapsed } from "../utils/formatUtils";
import { useSchedulesApi } from "../api/schedulesApi";
import { getSourceIcon } from "../constants/sourceIcons";
import { useSignalR } from "../hooks/useSignalR";
import { BackupStatus } from "../types/api";
import { formatRelativeTime, parseUtcDate } from "../utils/dateUtils";
import { getBackupOverallStatus } from "../utils/backupUtils";
import { EditableModuleTag } from "../components/EditableModuleTag";
import { EditIgnoredPathsDialog } from "../components/EditIgnoredPathsDialog";

interface State {
  loading: boolean;
  deletingId: string | null;
  runningId: string | null;
}

interface BackupStatusChipProps {
  backupId: string;
  scheduleToBackupMap: Record<string, string>;
  scheduleReports: Record<string, ScheduleReport>;
  t: (key: string, options?: Record<string, unknown>) => string;
}

function BackupStatusChip({
  backupId,
  scheduleToBackupMap,
  scheduleReports,
  t,
}: BackupStatusChipProps) {
  const status = getBackupOverallStatus(
    backupId,
    scheduleToBackupMap,
    scheduleReports,
  );

  const statusColors = {
    running: "info",
    scheduled: "warning",
    failed: "error",
    idle: "default",
  } as const;

  return (
    <Chip
      label={t(`backupStatus.${status}`)}
      size="small"
      color={statusColors[status]}
      sx={{ height: 20, fontSize: "0.7rem" }}
    />
  );
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
  });
  const [backups, setBackups] = useState<BackupItem[]>([]);
  const [scheduleReports, setScheduleReports] = useState<
    Record<string, ScheduleReport>
  >({});
  const [scheduleToBackupMap, setScheduleToBackupMap] = useState<
    Record<string, string>
  >({});

  useEffect(() => {
    let active = true;

    // Load backups and schedules immediately
    Promise.all([backupsApi.list(), schedulesApi.list()])
      .then(([backupList, schedulesList]) => {
        if (!active) return;
        setBackups(backupList);

        // Create mapping scheduleId -> backupId
        const mapping: Record<string, string> = {};
        schedulesList.forEach((schedule) => {
          mapping[schedule.id] = schedule.backupId;
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
  }, [backupsApi, schedulesApi]);

  // WebSocket listener for schedule reports
  useEffect(() => {
    if (!connection || !isConnected) return;

    const handler = (report: ScheduleReport) => {
      setScheduleReports((prev) => ({
        ...prev,
        [report.scheduleId]: report,
      }));
    };

    connection.on("ScheduleReport", handler);

    return () => {
      connection.off("ScheduleReport", handler);
    };
  }, [connection, isConnected]);

  const handleRename = async (backupId: string, newTag: string) => {
    await backupsApi.rename(backupId, newTag);
    setBackups((prev) =>
      prev.map((b) => (b.id === backupId ? { ...b, tag: newTag } : b)),
    );
  };

  const [editingIgnoredPathsId, setEditingIgnoredPathsId] = useState<
    string | null
  >(null);
  const [savingIgnoredPathsId, setSavingIgnoredPathsId] = useState<
    string | null
  >(null);

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
              // Find if backup has active schedule
              const aHasActiveSchedule = Object.entries(scheduleReports).some(
                ([scheduleId, r]) =>
                  scheduleToBackupMap[scheduleId] === a.id &&
                  r.status === BackupStatus.Running,
              );
              const bHasActiveSchedule = Object.entries(scheduleReports).some(
                ([scheduleId, r]) =>
                  scheduleToBackupMap[scheduleId] === b.id &&
                  r.status === BackupStatus.Running,
              );

              // Active backups first
              if (aHasActiveSchedule && !bHasActiveSchedule) return -1;
              if (!aHasActiveSchedule && bHasActiveSchedule) return 1;

              // Then sort by creation date (newest first)
              return (
                new Date(b.createdAt || 0).getTime() -
                new Date(a.createdAt || 0).getTime()
              );
            })
            .map((b) => (
              <Card
                key={b.id}
                sx={{
                  display: "flex",
                  alignItems: "center",
                  position: "relative",
                  minHeight: 80,
                }}
              >
                <CardContent
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 2,
                    width: "100%",
                    p: 2,
                    "&:last-child": { pb: 2 },
                  }}
                >
                  <Box
                    display="flex"
                    alignItems="center"
                    justifyContent="center"
                    flexDirection="column"
                  >
                    <Box
                      fontSize={36}
                      sx={{
                        width: 36,
                        height: 36,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                      }}
                    >
                      {getSourceIcon(b.source.backupModuleId)}
                    </Box>
                    <ArrowDownward />
                    <Box
                      fontSize={36}
                      sx={{
                        width: 36,
                        height: 36,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                      }}
                    >
                      {getSourceIcon(b.storage.backupModuleId)}
                    </Box>
                  </Box>
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Box
                      display="flex"
                      alignItems="center"
                      justifyContent="space-between"
                      gap={1}
                    >
                      <Box
                        display="flex"
                        alignItems="center"
                        gap={1}
                        minWidth={0}
                      >
                        <EditableModuleTag
                          tag={b.tag}
                          onRename={(newTag) => handleRename(b.id, newTag)}
                        />
                        <Divider orientation="vertical" flexItem />
                        <Typography
                          variant="caption"
                          sx={{ color: "text.secondary" }}
                        >
                          {b.source.tag} → {b.storage.tag}
                        </Typography>
                      </Box>
                      <BackupStatusChip
                        backupId={b.id}
                        scheduleToBackupMap={scheduleToBackupMap}
                        scheduleReports={scheduleReports}
                        t={t}
                      />
                    </Box>
                    <Box
                      display="flex"
                      alignItems="center"
                      gap={1}
                      flexWrap="wrap"
                      mt={0.5}
                    >
                      <Typography
                        variant="caption"
                        sx={{ color: "text.secondary" }}
                        title={parseUtcDate(b.createdAt)?.toLocaleString()}
                      >
                        {t("backups.createdAt", {
                          relativeTime: formatRelativeTime(b.createdAt, t),
                        })}
                      </Typography>
                      {b.snapshots &&
                        b.snapshots.length > 0 &&
                        (() => {
                          const lastSnapshot = b.snapshots
                            .filter((s) => s.completedAt)
                            .sort(
                              (a, b) =>
                                new Date(b.completedAt!).getTime() -
                                new Date(a.completedAt!).getTime(),
                            )[0];
                          const completedSnapshots = b.snapshots.filter(
                            (s) => s.completedAt,
                          );

                          return (
                            <>
                              {lastSnapshot && (
                                <>
                                  <Divider orientation="vertical" flexItem />
                                  <Typography
                                    variant="caption"
                                    sx={{ color: "text.secondary" }}
                                    title={parseUtcDate(
                                      lastSnapshot.completedAt,
                                    )?.toLocaleString()}
                                  >
                                    {t("backups.lastBackup", {
                                      relativeTime: formatRelativeTime(
                                        lastSnapshot.completedAt,
                                        t,
                                      ),
                                    })}
                                  </Typography>
                                </>
                              )}
                              <Divider orientation="vertical" flexItem />
                              <Typography
                                variant="caption"
                                sx={{ color: "text.secondary" }}
                              >
                                {t("backups.snapshots", {
                                  count: completedSnapshots.length,
                                })}
                              </Typography>
                              {lastSnapshot && (
                                <>
                                  <Divider orientation="vertical" flexItem />
                                  <Typography
                                    variant="caption"
                                    sx={{ color: "text.secondary" }}
                                  >
                                    {t("backups.totalFiles", {
                                      count: lastSnapshot.filesCount,
                                    })}
                                  </Typography>
                                  <Divider orientation="vertical" flexItem />
                                  <Typography
                                    variant="caption"
                                    sx={{ color: "text.secondary" }}
                                  >
                                    {t("backups.totalSize", {
                                      size: formatSize(lastSnapshot.totalSize),
                                    })}
                                  </Typography>
                                </>
                              )}
                            </>
                          );
                        })()}
                    </Box>
                    {(() => {
                      // Find report for this backup using scheduleToBackupMap
                      const report = Object.entries(scheduleReports).find(
                        ([scheduleId, r]) =>
                          scheduleToBackupMap[scheduleId] === b.id &&
                          r.status === BackupStatus.Running,
                      )?.[1];
                      if (report) {
                        const progress =
                          report.total > 0
                            ? (report.processed / report.total) * 100
                            : 0;
                        return (
                          <Box sx={{ mt: 1 }}>
                            <LinearProgress
                              variant="determinate"
                              value={progress}
                              sx={{ height: 4, borderRadius: 1, mb: 0.5 }}
                            />
                            <Box
                              display="flex"
                              justifyContent="space-between"
                              alignItems="center"
                            >
                              <Tooltip
                                title={report.message}
                                placement="top"
                                arrow
                              >
                                <Typography
                                  variant="caption"
                                  color="text.secondary"
                                  noWrap
                                  sx={{ flex: 1, mr: 1 }}
                                >
                                  {report.message}
                                </Typography>
                              </Tooltip>
                              <Typography
                                variant="caption"
                                color="text.secondary"
                                sx={{ whiteSpace: "nowrap" }}
                              >
                                {report.processed.toLocaleString()} /{" "}
                                {report.total.toLocaleString()} •{" "}
                                {progress.toFixed(0)}%
                              </Typography>
                            </Box>
                            <Box display="flex" gap={2} mt={0.5}>
                              <Typography
                                variant="caption"
                                color="text.secondary"
                              >
                                {formatSpeed(report.speed)}
                              </Typography>
                              {report.elapsed && (
                                <Box
                                  display="flex"
                                  alignItems="center"
                                  gap={0.5}
                                >
                                  <AccessTime
                                    sx={{ fontSize: 14 }}
                                    color="action"
                                  />
                                  <Typography
                                    variant="caption"
                                    color="text.secondary"
                                  >
                                    {formatElapsed(report.elapsed)}
                                  </Typography>
                                </Box>
                              )}
                            </Box>
                          </Box>
                        );
                      }
                      return null;
                    })()}
                  </Box>
                  <Divider orientation="vertical" flexItem />
                  <Box display="flex" flexDirection="column">
                    <Tooltip title={t("backups.showSnapshots")} placement="top">
                      <IconButton
                        size="small"
                        aria-label={t("backups.showSnapshots")}
                        onClick={() => navigate(`/backups/${b.id}/snapshots`)}
                      >
                        <BackupTable color="warning" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={t("backups.ignoredPaths")} placement="top">
                      <IconButton
                        size="small"
                        aria-label={t("backups.ignoredPaths")}
                        disabled={savingIgnoredPathsId === b.id}
                        onClick={(e) => {
                          e.stopPropagation();
                          setEditingIgnoredPathsId(b.id);
                        }}
                      >
                        <FilterAlt color="info" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={t("backups.runOnce")} placement="top">
                      <IconButton
                        size="small"
                        aria-label={t("backups.runOnce")}
                        disabled={state.runningId === b.id}
                        onClick={async (e) => {
                          e.stopPropagation();
                          try {
                            setState((s) => ({ ...s, runningId: b.id }));
                            await schedulesApi.create({
                              backupId: b.id,
                              startAt: new Date().toISOString(),
                            });
                            navigate("/schedules");
                          } finally {
                            setState((s) => ({ ...s, runningId: null }));
                          }
                        }}
                      >
                        {state.runningId === b.id ? (
                          <CircularProgress size={20} />
                        ) : (
                          <PlayArrow color="success" />
                        )}
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={t("backups.deleteTooltip")} placement="top">
                      <IconButton
                        size="small"
                        aria-label={t("common.delete")}
                        disabled={state.deletingId === b.id}
                        onClick={async (e) => {
                          e.stopPropagation();
                          const result = await confirm({
                            title: t("backups.deleteTitle"),
                            description: t("backups.deleteText"),
                            confirmationText: t("common.delete"),
                            cancellationText: t("common.cancel"),
                            confirmationButtonProps: { color: "error" },
                          });
                          if (result.confirmed) {
                            setState((s) => ({ ...s, deletingId: b.id }));
                            await backupsApi.delete(b.id);
                            setBackups((prev) =>
                              prev.filter((x) => x.id !== b.id),
                            );
                            setState((s) => ({ ...s, deletingId: null }));
                          }
                        }}
                      >
                        <DeleteOutline color="primary" />
                      </IconButton>
                    </Tooltip>
                  </Box>
                </CardContent>
              </Card>
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
