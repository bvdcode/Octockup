import {
  Box,
  Card,
  Stack,
  Alert,
  Divider,
  Typography,
  CardContent,
  CircularProgress,
  IconButton,
  Button,
  Tooltip,
} from "@mui/material";
import { DeleteOutline, AddCircleOutline, PlayArrow } from "@mui/icons-material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { useBackupsApi } from "../api/backupsApi";
import { useModulesApi } from "../api/modulesApi";
import { useSchedulesApi } from "../api/schedulesApi";
import type { BackupItem, Module } from "../types/api";
import { ModuleDestination } from "../types/api";
import { getSourceIcon } from "../constants/sourceIcons";
import { confirm } from "material-ui-confirm";

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
  const modulesApi = useModulesApi();
  const schedulesApi = useSchedulesApi();
  const [state, setState] = useState<State>({ loading: true, error: null, deletingId: null, runningId: null });
  const [backups, setBackups] = useState<BackupItem[]>([]);
  const [sources, setSources] = useState<Module[]>([]);
  const [storages, setStorages] = useState<Module[]>([]);

  useEffect(() => {
    let active = true;
    Promise.all([
      backupsApi.list(),
      modulesApi.list(),
    ])
      .then(([backupList, modules]) => {
        if (!active) return;
        setBackups(backupList);
        setSources(modules.filter(m => m.destination === ModuleDestination.Source));
        setStorages(modules.filter(m => m.destination === ModuleDestination.Target));
        setState(s => ({ ...s, loading: false }));
      })
      .catch(e => {
        if (!active) return;
        setState(s => ({ ...s, loading: false, error: e?.message || "Failed to load backups" }));
      });
    return () => { active = false; };
  }, [backupsApi, modulesApi]);

  if (state.loading) {
    return <Box display="flex" justifyContent="center" p={4}><CircularProgress /></Box>;
  }

  if (state.error) {
    return <Box p={2}><Alert severity="error">{state.error}</Alert></Box>;
  }

  return (
    <Stack spacing={3}>
      <Box display="flex" alignItems="center" justifyContent="space-between">
        <Typography variant="h5">{t("backups.title")}</Typography>
        <Button variant="contained" startIcon={<AddCircleOutline />} onClick={() => navigate("/backups/new")}>{t("backups.newBackup")}</Button>
      </Box>
      {backups.length === 0 ? (
        <Card variant="outlined"><CardContent><Typography color="text.secondary">{t("backups.noBackups")}</Typography></CardContent></Card>
      ) : (
        <Stack direction="row" spacing={2} flexWrap="wrap">
          {backups.map(b => (
            <Card
              key={b.id}
              sx={{ width: 240, height: 170, flex: "0 0 240px", display: "flex", alignItems: "stretch", position: "relative" }}
            >
              <Box sx={{ position: "absolute", top: 4, right: 4, display: "flex", flexDirection: "column", gap: 0.5 }}>
                <Tooltip title={t("backups.deleteTooltip", { defaultValue: "Delete backup" })} placement="left">
                  <span>
                    <IconButton
                      size="small"
                      aria-label={t("common.delete")}
                      disabled={state.deletingId === b.id}
                      onClick={async (e) => {
                        e.stopPropagation();
                        const result = await confirm({
                          title: t("backups.deleteTitle", { defaultValue: "Delete backup" }),
                          description: t("backups.deleteText", { defaultValue: "This action is permanent!" }),
                          confirmationText: t("common.delete", { defaultValue: "Delete" }),
                          cancellationText: t("common.cancel", { defaultValue: "Cancel" }),
                          confirmationButtonProps: { color: "error" },
                        });
                        if (result.confirmed) {
                          setState(s => ({ ...s, deletingId: b.id }));
                          await backupsApi.delete(b.id);
                          setBackups(prev => prev.filter(x => x.id !== b.id));
                          setState(s => ({ ...s, deletingId: null }));
                        }
                      }}
                    >
                      <DeleteOutline fontSize="small" color="primary" />
                    </IconButton>
                  </span>
                </Tooltip>
                <Tooltip title={t("backups.runOnce", { defaultValue: "Run once" })} placement="left">
                  <span>
                    <IconButton
                      size="small"
                      aria-label={t("backups.runOnce", { defaultValue: "Run once" })}
                      disabled={state.runningId === b.id}
                      onClick={async (e) => {
                        e.stopPropagation();
                        try {
                          setState(s => ({ ...s, runningId: b.id }));
                          await schedulesApi.create({ backupId: b.id, startAt: new Date().toISOString() });
                        } finally {
                          setState(s => ({ ...s, runningId: null }));
                        }
                      }}
                    >
                      {state.runningId === b.id ? <CircularProgress size={16} /> : <PlayArrow fontSize="small" color="success" />}
                    </IconButton>
                  </span>
                </Tooltip>
              </Box>
              <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 0.75, justifyContent: "space-between", height: "100%", p: 2 }}>
                <Box display="flex" alignItems="center" gap={1} mt={1}>
                  <Box fontSize={32}>{getSourceIcon(b.source.backupModuleId)}</Box>
                  <Typography variant="caption">→</Typography>
                  <Box fontSize={32}>{getSourceIcon(b.storage.backupModuleId)}</Box>
                </Box>
                <Typography variant="subtitle2" noWrap title={b.tag} sx={{ textAlign: "center", maxWidth: 180 }}>{b.tag}</Typography>
                <Typography variant="caption" sx={{ color: "text.secondary", textAlign: "center" }}>{b.source.tag} → {b.storage.tag}</Typography>
              </CardContent>
            </Card>
          ))}
        </Stack>
      )}
      <Divider />
      <Box>
        <Typography variant="h6" gutterBottom>{t("backups.modulesTitle")}</Typography>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          {sources.map(s => (
            <Card key={s.id} sx={{ width: 140, height: 120, flex: "0 0 140px", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 0.5, p: 2 }}>
                <Box fontSize={28}>{getSourceIcon(s.backupModuleId)}</Box>
                <Typography variant="caption" noWrap>{s.tag}</Typography>
              </CardContent>
            </Card>
          ))}
          {storages.map(s => (
            <Card key={s.id} sx={{ width: 140, height: 120, flex: "0 0 140px", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 0.5, p: 2 }}>
                <Box fontSize={28}>{getSourceIcon(s.backupModuleId)}</Box>
                <Typography variant="caption" noWrap>{s.tag}</Typography>
              </CardContent>
            </Card>
          ))}
        </Stack>
      </Box>
    </Stack>
  );
}
