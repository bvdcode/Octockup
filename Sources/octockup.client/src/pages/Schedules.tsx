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
} from "@mui/material";
import {
  StopCircle,
  DeleteOutline,
  AddCircleOutline,
} from "@mui/icons-material";
import { BackupStatus } from "../types/api";
import { useEffect, useState } from "react";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { ScheduleItem } from "../types/api";
import { parseUtcDate } from "../utils/dateUtils";
import { useSchedulesApi } from "../api/schedulesApi";
import { getSourceIcon } from "../constants/sourceIcons";

interface State {
  loading: boolean;
  error: string | null;
  deletingId: string | null;
  cancelingId: string | null;
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
  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    deletingId: null,
    cancelingId: null,
  });
  const [items, setItems] = useState<ScheduleItem[]>([]);

  useEffect(() => {
    let active = true;
    api
      .list()
      .then((data) => {
        if (!active) return;
        setItems(data);
        setState({ loading: false, error: null });
      })
      .catch((e) => {
        if (!active) return;
        setState({
          loading: false,
          error: e?.message || "Failed to load schedules",
        });
      });
    return () => {
      active = false;
    };
  }, [api]);

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
          {items.map((it) => (
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
                <Box display="flex" alignItems="center" gap={1.5}>
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
                  <Typography variant="h6" sx={{ mx: 0.5 }}>
                    →
                  </Typography>
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
                  <Typography variant="subtitle1" noWrap title={it.backup.tag}>
                    {it.backup.tag}
                  </Typography>
                  <Typography
                    variant="caption"
                    sx={{ color: "text.secondary" }}
                  >
                    {it.backup.source.tag} → {it.backup.storage.tag}
                  </Typography>
                </Box>
                <Box
                  display="flex"
                  flexDirection="column"
                  gap={0.5}
                  alignItems="flex-end"
                  minWidth={150}
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
                  <Typography
                    variant="caption"
                    sx={{ color: "text.secondary" }}
                  >
                    {t("schedules.nextRun.label", { defaultValue: "Next run" })}
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
                              ? parseInt(parts[0]) * 60 + parseInt(parts[1])
                              : 0;
                          return ` • ${t("schedules.everyMinutes", {
                            count: minutes,
                          })}`;
                        })()
                      : ""}
                  </Typography>
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
          ))}
        </Stack>
      )}
      <Divider />
    </Stack>
  );
}
