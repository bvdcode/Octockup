import {
  Box,
  Card,
  Chip,
  Stack,
  Alert,
  Button,
  Tooltip,
  Divider,
  Typography,
  IconButton,
  CardContent,
  CircularProgress,
  LinearProgress,
} from "@mui/material";
import {
  StopCircle,
  DeleteOutline,
  AddCircleOutline,
  ArrowRightAlt,
} from "@mui/icons-material";
import { BackupStatus } from "../types/api";
import { useEffect, useState } from "react";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { useSignalR } from "../hooks/useSignalR";
import type { ScheduleItem, ScheduleReport } from "../types/api";
import { parseUtcDate } from "../utils/dateUtils";
import { useSchedulesApi } from "../api/schedulesApi";
import { getSourceIcon } from "../constants/sourceIcons";

interface State {
  loading: boolean;
  error: string | null;
  deletingId: string | null;
  cancelingId: string | null;
}

function formatSpeed(bytesPerSecond: number): string {
  const mbPerSecond = bytesPerSecond / (1024 * 1024);
  if (mbPerSecond < 0.01) {
    const kbPerSecond = bytesPerSecond / 1024;
    return `${kbPerSecond.toFixed(2)} KB/s`;
  }
  return `${mbPerSecond.toFixed(2)} MB/s`;
}

function formatElapsed(elapsed?: string): string {
  if (!elapsed) return "";
  // Parse TimeSpan format: "HH:MM:SS.mmmmmmm" or "DD.HH:MM:SS.mmmmmmm"
  const parts = elapsed.split(":");
  if (parts.length < 3) return elapsed;

  let hours = 0;
  let minutes = 0;
  let seconds = 0;

  if (parts[0].includes(".")) {
    // Format: DD.HH:MM:SS
    const dayHour = parts[0].split(".");
    const days = parseInt(dayHour[0]);
    hours = parseInt(dayHour[1]) + days * 24;
    minutes = parseInt(parts[1]);
    seconds = Math.floor(parseFloat(parts[2]));
  } else {
    // Format: HH:MM:SS
    hours = parseInt(parts[0]);
    minutes = parseInt(parts[1]);
    seconds = Math.floor(parseFloat(parts[2]));
  }

  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  } else if (minutes > 0) {
    return `${minutes}m ${seconds}s`;
  } else {
    return `${seconds}s`;
  }
}

function statusColor(
  status: BackupStatus,
): "default" | "success" | "error" | "warning" | "info" {
  switch (status) {
    case BackupStatus.Completed:
      return "success";
    case BackupStatus.Running:
      return "info";
    case BackupStatus.Failed:
      return "error";
    case BackupStatus.Created:
    default:
      return "default";
  }
}

function calculateNextRun(
  item: ScheduleItem,
  t: (key: string, options?: Record<string, unknown>) => string,
): string {
  const now = new Date();
  const startAt = parseUtcDate(item.startAt)!;

  // If no interval (one-time schedule)
  if (!item.interval) {
    if (
      item.status === BackupStatus.Completed ||
      item.status === BackupStatus.Failed
    ) {
      return t("schedules.nextRun.never", { defaultValue: "Never" });
    }
    if (startAt > now) {
      const diff = startAt.getTime() - now.getTime();
      if (diff < 60000)
        return t("schedules.nextRun.soon", { defaultValue: "Soon" });
      if (diff < 3600000)
        return t("schedules.nextRun.inMinutes", {
          defaultValue: "In {{count}} minutes",
          count: Math.floor(diff / 60000),
        });
      if (diff < 86400000)
        return t("schedules.nextRun.inHours", {
          defaultValue: "In {{count}} hours",
          count: Math.floor(diff / 3600000),
        });
      return t("schedules.nextRun.scheduled", { defaultValue: "Scheduled" });
    }
    return t("schedules.nextRun.soon", { defaultValue: "Soon" });
  }

  // Parse interval
  const parts = String(item.interval).split(":");
  const intervalMinutes =
    parts.length >= 2 ? parseInt(parts[0]) * 60 + parseInt(parts[1]) : 0;
  if (intervalMinutes === 0)
    return t("schedules.nextRun.unknown", { defaultValue: "Unknown" });

  // Calculate next run based on last run or start time
  let nextRun: Date;
  if (
    item.finishedAt &&
    (item.status === BackupStatus.Completed ||
      item.status === BackupStatus.Failed)
  ) {
    nextRun = new Date(
      parseUtcDate(item.finishedAt)!.getTime() + intervalMinutes * 60000,
    );
  } else if (item.status === BackupStatus.Running) {
    // Currently running, next run is after it finishes
    return t("schedules.nextRun.afterCurrent", {
      defaultValue: "After current run",
    });
  } else {
    nextRun = new Date(startAt.getTime() + intervalMinutes * 60000);
  }

  const diff = nextRun.getTime() - now.getTime();
  if (diff < 0) return t("schedules.nextRun.soon", { defaultValue: "Soon" });
  if (diff < 60000)
    return t("schedules.nextRun.soon", { defaultValue: "Soon" });
  if (diff < 3600000)
    return t("schedules.nextRun.inMinutes", {
      defaultValue: "In {{count}} minutes",
      count: Math.floor(diff / 60000),
    });
  if (diff < 86400000)
    return t("schedules.nextRun.inHours", {
      defaultValue: "In {{count}} hours",
      count: Math.floor(diff / 3600000),
    });
  if (diff < 172800000)
    return t("schedules.nextRun.tomorrow", { defaultValue: "Tomorrow" });
  if (diff < 604800000)
    return t("schedules.nextRun.inDays", {
      defaultValue: "In {{count}} days",
      count: Math.floor(diff / 86400000),
    });
  if (diff < 2592000000)
    return t("schedules.nextRun.inWeeks", {
      defaultValue: "In {{count}} weeks",
      count: Math.floor(diff / 604800000),
    });
  return t("schedules.nextRun.inMonths", {
    defaultValue: "In {{count}} months",
    count: Math.floor(diff / 2592000000),
  });
}

export default function SchedulesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useSchedulesApi();
  const { connection, isConnected } = useSignalR("/api/v1/event-hub");
  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    deletingId: null,
    cancelingId: null,
  });
  const [items, setItems] = useState<ScheduleItem[]>([]);
  const [scheduleReports, setScheduleReports] = useState<
    Record<string, ScheduleReport>
  >({});

  useEffect(() => {
    let active = true;
    api
      .list()
      .then((data) => {
        if (!active) return;
        setItems(data);
        setState({
          loading: false,
          error: null,
          deletingId: null,
          cancelingId: null,
        });
      })
      .catch((e) => {
        if (!active) return;
        setState({
          loading: false,
          error: e?.message || "Failed to load schedules",
          deletingId: null,
          cancelingId: null,
        });
      });
    return () => {
      active = false;
    };
  }, [api]);

  // WebSocket listener for schedule reports
  useEffect(() => {
    if (!connection || !isConnected) return;

    const handler = (report: ScheduleReport) => {
      setScheduleReports((prev) => ({
        ...prev,
        [report.scheduleId]: report,
      }));

      // Update schedule status in items list
      setItems((prev) =>
        prev.map((item) =>
          item.id === report.scheduleId
            ? { ...item, status: report.status }
            : item,
        ),
      );
    };

    connection.on("ScheduleReport", handler);

    return () => {
      connection.off("ScheduleReport", handler);
    };
  }, [connection, isConnected]);

  if (state.loading) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }
  if (state.error) {
    return (
      <Box p={2}>
        <Alert severity="error">{state.error}</Alert>
      </Box>
    );
  }

  const hasItems = items.length > 0;

  return (
    <Stack spacing={3}>
      <Box display="flex" alignItems="center" justifyContent="space-between">
        <Typography variant="h5">{t("schedules.title")}</Typography>
        <Button
          variant="contained"
          startIcon={<AddCircleOutline />}
          onClick={() => navigate("/schedules/new")}
        >
          {t("schedules.newSchedule")}
        </Button>
      </Box>
      {!hasItems ? (
        <Card>
          <CardContent>
            <Typography color="text.secondary">
              {t("schedules.noSchedules")}
            </Typography>
          </CardContent>
        </Card>
      ) : (
        <Stack spacing={2}>
          {items.map((it) => {
            const report = scheduleReports[it.id];
            const progress =
              report && report.total > 0
                ? (report.processed / report.total) * 100
                : 0;

            return (
              <Card key={it.id} sx={{ display: "flex" }}>
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
                      {getSourceIcon(it.backup.source.backupModuleId)}
                    </Box>
                    <ArrowRightAlt />
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
                      {getSourceIcon(it.backup.storage.backupModuleId)}
                    </Box>
                  </Box>
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography
                      variant="subtitle1"
                      noWrap
                      title={it.backup.tag}
                    >
                      {it.backup.tag}
                    </Typography>
                    <Typography
                      variant="caption"
                      sx={{ color: "text.secondary" }}
                    >
                      {it.backup.source.tag} → {it.backup.storage.tag}
                    </Typography>
                    {report && it.status === BackupStatus.Running && (
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
                          <Typography
                            variant="caption"
                            color="text.secondary"
                            noWrap
                            sx={{ flex: 1, mr: 1 }}
                          >
                            {report.message}
                          </Typography>
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
                          <Typography variant="caption" color="text.secondary">
                            {formatSpeed(report.speed)}
                          </Typography>
                          {report.elapsed && (
                            <Typography
                              variant="caption"
                              color="text.secondary"
                            >
                              ⏱ {formatElapsed(report.elapsed)}
                            </Typography>
                          )}
                        </Box>
                      </Box>
                    )}
                  </Box>
                  <Box
                    display="flex"
                    flexDirection="column"
                    gap={0.5}
                    alignItems="flex-end"
                    minWidth={120}
                  >
                    <Chip
                      size="small"
                      label={t(
                        `schedules.status.${BackupStatus[
                          it.status
                        ].toLowerCase()}`,
                      )}
                      color={statusColor(it.status)}
                    />
                    {it.status !== BackupStatus.Running && (
                      <>
                        <Typography
                          variant="caption"
                          sx={{ color: "text.secondary" }}
                        >
                          {t("schedules.nextRun.label", {
                            defaultValue: "Next run",
                          })}
                          : {calculateNextRun(it, t)}
                        </Typography>
                        <Typography
                          variant="caption"
                          sx={{ color: "text.secondary", fontSize: "0.65rem" }}
                        >
                          {parseUtcDate(it.startAt)!.toLocaleString()}
                          {it.interval
                            ? (() => {
                                const parts = String(it.interval).split(":");
                                const minutes =
                                  parts.length >= 2
                                    ? parseInt(parts[0]) * 60 +
                                      parseInt(parts[1])
                                    : 0;
                                return ` • ${t("schedules.everyMinutes", {
                                  count: minutes,
                                })}`;
                              })()
                            : ""}
                        </Typography>
                      </>
                    )}
                  </Box>
                  <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />
                  <Box display="flex" flexDirection="column" gap={0.5}>
                    <Tooltip
                      title={t("schedules.deleteTooltip", {
                        defaultValue: "Delete schedule",
                      })}
                      placement="top"
                    >
                      <span>
                        <IconButton
                          size="small"
                          aria-label={t("common.delete")}
                          disabled={state.deletingId === it.id}
                          onClick={async (e) => {
                            e.stopPropagation();
                            const result = await confirm({
                              title: t("schedules.deleteTitle", {
                                defaultValue: "Delete schedule",
                              }),
                              description: t("schedules.deleteText", {
                                defaultValue: "This action is permanent!",
                              }),
                              confirmationText: t("common.delete", {
                                defaultValue: "Delete",
                              }),
                              cancellationText: t("common.cancel", {
                                defaultValue: "Cancel",
                              }),
                              confirmationButtonProps: { color: "error" },
                            });
                            if (result.confirmed) {
                              setState((s) => ({ ...s, deletingId: it.id }));
                              await api.delete(it.id);
                              setItems((prev) =>
                                prev.filter((x) => x.id !== it.id),
                              );
                              setState((s) => ({ ...s, deletingId: null }));
                            }
                          }}
                        >
                          <DeleteOutline color="primary" />
                        </IconButton>
                      </span>
                    </Tooltip>
                    <Tooltip
                      title={t("schedules.stopTooltip", {
                        defaultValue: "Stop running schedule",
                      })}
                      placement="top"
                    >
                      <span>
                        <IconButton
                          size="small"
                          aria-label={t("schedules.stop", {
                            defaultValue: "Stop",
                          })}
                          disabled={
                            it.status !== BackupStatus.Running ||
                            state.cancelingId === it.id
                          }
                          onClick={async (e) => {
                            e.stopPropagation();
                            setState((s) => ({ ...s, cancelingId: it.id }));
                            try {
                              await api.cancel(it.id);
                              // Refresh list or update status locally
                              setItems((prev) =>
                                prev.map((x) =>
                                  x.id === it.id
                                    ? { ...x, status: BackupStatus.Failed }
                                    : x,
                                ),
                              );
                            } finally {
                              setState((s) => ({ ...s, cancelingId: null }));
                            }
                          }}
                        >
                          <StopCircle />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </Box>
                </CardContent>
              </Card>
            );
          })}
        </Stack>
      )}
      <Divider />
    </Stack>
  );
}
