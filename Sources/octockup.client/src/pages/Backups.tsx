import {
  Box,
  Card,
  Stack,
  Alert,
  Button,
  Tooltip,
  Typography,
  IconButton,
  CardContent,
  CircularProgress,
  Divider,
} from "@mui/material";
import {
  PlayArrow,
  DeleteOutline,
  ArrowRightAlt,
  AddCircleOutline,
  ArrowDownward,
  BackupTable,
} from "@mui/icons-material";
import { useEffect, useState } from "react";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { BackupItem } from "../types/api";
import { useBackupsApi } from "../api/backupsApi";
import { useSchedulesApi } from "../api/schedulesApi";
import { useSnapshotsApi } from "../api/snapshotsApi";
import { getSourceIcon } from "../constants/sourceIcons";
import { formatSize } from "../utils/formatUtils";
import { useSnapshotsStore } from "../stores/snapshotsStore";

interface State {
  loading: boolean;
  error: string | null;
  deletingId: string | null;
  runningId: string | null;
}

export default function BackupsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const backupsApi = useBackupsApi();
  const schedulesApi = useSchedulesApi();
  const snapshotsApi = useSnapshotsApi();
  const { snapshots, setSnapshots } = useSnapshotsStore();
  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    deletingId: null,
    runningId: null,
  });
  const [backups, setBackups] = useState<BackupItem[]>([]);

  useEffect(() => {
    let active = true;
    
    // Load backups immediately
    backupsApi
      .list()
      .then((backupList) => {
        if (!active) return;
        setBackups(backupList);
        setState((s) => ({ ...s, loading: false }));

        // Always fetch fresh snapshots in background for each backup
        backupList.forEach((backup) => {
          snapshotsApi
            .listByBackup(backup.id)
            .then((data) => {
              if (active) {
                setSnapshots(backup.id, data);
              }
            })
            .catch(() => {
              if (active) {
                setSnapshots(backup.id, []);
              }
            });
        });
      })
      .catch((e) => {
        if (!active) return;
        setState((s) => ({
          ...s,
          loading: false,
          error: e?.message || "Failed to load backups",
        }));
      });
      
    return () => {
      active = false;
    };
  }, [backupsApi, snapshotsApi, setSnapshots]);

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
          {backups.map((b) => (
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
                  flexDirection={{
                    xs: "column",
                    sm: "row",
                  }}
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
                  <ArrowRightAlt
                    sx={{
                      display: { xs: "none", sm: "block" },
                      mx: 1,
                      my: { xs: 1, sm: 0 },
                    }}
                  />
                  <ArrowDownward
                    sx={{
                      display: { xs: "block", sm: "none" },
                    }}
                  />
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
                  <Typography variant="subtitle1" noWrap title={b.tag}>
                    {b.tag}
                  </Typography>
                  <Typography
                    variant="caption"
                    sx={{ color: "text.secondary" }}
                  >
                    {b.source.tag} → {b.storage.tag}
                  </Typography>
                  {snapshots[b.id] && snapshots[b.id].length > 0 && (
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        display: "block",
                        mt: 0.5,
                      }}
                    >
                      {t("backups.snapshots", {
                        count: snapshots[b.id].length,
                      })}{" "}
                      •{" "}
                      {t("backups.totalFiles", {
                        count: snapshots[b.id].reduce(
                          (sum, s) => sum + s.filesCount,
                          0,
                        ),
                      })}{" "}
                      •{" "}
                      {t("backups.totalSize", {
                        size: formatSize(
                          snapshots[b.id].reduce(
                            (sum, s) => sum + s.totalSize,
                            0,
                          ),
                        ),
                      })}
                    </Typography>
                  )}
                </Box>
                <Divider orientation="vertical" flexItem />
                <Box display="flex" flexDirection="column">
                  <Tooltip title={t("backups.showSnapshots")} placement="top">
                    <IconButton
                      size="medium"
                      aria-label={t("backups.showSnapshots")}
                      onClick={() => navigate(`/backups/${b.id}/snapshots`)}
                    >
                      <BackupTable color="warning" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title={t("backups.runOnce")} placement="top">
                    <IconButton
                      size="medium"
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
                      size="medium"
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
    </Stack>
  );
}
