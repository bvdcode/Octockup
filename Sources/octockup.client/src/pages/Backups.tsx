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
} from "@mui/material";
import {
  ArrowRight,
  PlayArrow,
  DeleteOutline,
  AddCircleOutline,
} from "@mui/icons-material";
import { useEffect, useState } from "react";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { BackupItem } from "../types/api";
import { useBackupsApi } from "../api/backupsApi";
import { useSchedulesApi } from "../api/schedulesApi";
import { getSourceIcon } from "../constants/sourceIcons";

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
  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    deletingId: null,
    runningId: null,
  });
  const [backups, setBackups] = useState<BackupItem[]>([]);

  useEffect(() => {
    let active = true;
    backupsApi
      .list()
      .then((backupList) => {
        if (!active) return;
        setBackups(backupList);
        setState((s) => ({ ...s, loading: false }));
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
  }, [backupsApi]);

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
                <Box display="flex" alignItems="center" gap={0.5}>
                  <Box
                    fontSize={40}
                    sx={{
                      width: 40,
                      height: 40,
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                    }}
                  >
                    {getSourceIcon(b.source.backupModuleId)}
                  </Box>
                  <ArrowRight />
                  <Box
                    fontSize={40}
                    sx={{
                      width: 40,
                      height: 40,
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
                </Box>
                <Box display="flex" gap={1}>
                  <Tooltip
                    title={t("backups.runOnce", { defaultValue: "Run once" })}
                    placement="top"
                  >
                    <span>
                      <IconButton
                        size="medium"
                        aria-label={t("backups.runOnce", {
                          defaultValue: "Run once",
                        })}
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
                    </span>
                  </Tooltip>
                  <Tooltip
                    title={t("backups.deleteTooltip", {
                      defaultValue: "Delete backup",
                    })}
                    placement="top"
                  >
                    <span>
                      <IconButton
                        size="medium"
                        aria-label={t("common.delete")}
                        disabled={state.deletingId === b.id}
                        onClick={async (e) => {
                          e.stopPropagation();
                          const result = await confirm({
                            title: t("backups.deleteTitle", {
                              defaultValue: "Delete backup",
                            }),
                            description: t("backups.deleteText", {
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
                    </span>
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
