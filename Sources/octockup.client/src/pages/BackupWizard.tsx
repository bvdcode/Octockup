import {
  Box,
  Stack,
  Alert,
  Button,
  Divider,
  Typography,
  CircularProgress,
  TextField,
  Card,
  CardContent,
  MenuItem,
  Chip,
  Paper,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { useBackupsApi } from "../api/backupsApi";
import { useModulesApi } from "../api/modulesApi";
import type { Module, CreateBackupRequest } from "../types/api";
import { ModuleDestination } from "../types/api";
import { getSourceIcon } from "../constants/sourceIcons";

interface State {
  loading: boolean;
  error: string | null;
  creating: boolean;
  createError: string | null;
}

export default function BackupWizard() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const backupsApi = useBackupsApi();
  const modulesApi = useModulesApi();

  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    creating: false,
    createError: null,
  });
  const [modules, setModules] = useState<Module[]>([]);

  const [sourceId, setSourceId] = useState<string>("");
  const [storageId, setStorageId] = useState<string>("");
  const [tag, setTag] = useState<string>("");
  const [userEditedTag, setUserEditedTag] = useState<boolean>(false);
  const [ignoredPathsInput, setIgnoredPathsInput] = useState<string>("");

  useEffect(() => {
    let active = true;
    modulesApi
      .list()
      .then((data) => {
        if (!active) return;
        setModules(data);
        setState((s) => ({ ...s, loading: false }));
      })
      .catch((e) => {
        if (!active) return;
        setState((s) => ({
          ...s,
          loading: false,
          error: e?.message || "Failed to load modules",
        }));
      });
    return () => {
      active = false;
    };
  }, [modulesApi]);

  const sources = useMemo(
    () => modules.filter((m) => m.destination === ModuleDestination.Source),
    [modules],
  );
  const storages = useMemo(
    () => modules.filter((m) => m.destination === ModuleDestination.Target),
    [modules],
  );

  const autoTag = useMemo(() => {
    if (sourceId && storageId) {
      const sourceMod = modules.find((m) => m.id === sourceId);
      const storageMod = modules.find((m) => m.id === storageId);
      if (sourceMod && storageMod) {
        return `${sourceMod.tag} ${t("backupWizard.to")} ${storageMod.tag}`;
      }
    }
    return "";
  }, [sourceId, storageId, modules, t]);

  const displayTag = useMemo(() => {
    if (userEditedTag) {
      return tag;
    }
    return autoTag || tag;
  }, [tag, autoTag, userEditedTag]);

  const canCreate = useMemo(
    () => !!sourceId && !!storageId && !!displayTag,
    [sourceId, storageId, displayTag],
  );

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
      <Typography variant="h5">{t("backupWizard.title")}</Typography>
      {state.createError && <Alert severity="error">{state.createError}</Alert>}
      <Card>
        <CardContent>
          <Stack spacing={3}>
            <Box
              display="flex"
              gap={2}
              alignItems="stretch"
              justifyContent="center"
              sx={{ flexDirection: { xs: "column", md: "row" } }}
            >
              <Paper
                variant="outlined"
                sx={{
                  p: 3,
                  flex: "1 1 auto",
                  textAlign: "center",
                  minWidth: 280,
                  display: "flex",
                  flexDirection: "column",
                  justifyContent: "space-between",
                  alignItems: "center",
                }}
              >
                <Box
                  sx={{
                    fontSize: 96,
                    lineHeight: 1,
                    mb: 2,
                    minHeight: 96,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  {getSourceIcon(
                    modules.find((m) => m.id === sourceId)?.backupModuleId ||
                      "",
                  )}
                </Box>
                <TextField
                  select
                  label={t("backupWizard.source")}
                  value={sourceId}
                  onChange={(e) => setSourceId(e.target.value)}
                  fullWidth
                  sx={{ maxWidth: 400 }}
                >
                  {sources.map((s) => (
                    <MenuItem key={s.id} value={s.id}>
                      {s.tag}
                    </MenuItem>
                  ))}
                </TextField>
              </Paper>
              <Box
                display="flex"
                alignItems="center"
                justifyContent="center"
                fontSize={48}
                sx={{ minWidth: 48, py: { xs: 2, md: 0 } }}
              >
                →
              </Box>
              <Paper
                variant="outlined"
                sx={{
                  p: 3,
                  flex: "1 1 auto",
                  textAlign: "center",
                  minWidth: 280,
                  display: "flex",
                  flexDirection: "column",
                  justifyContent: "space-between",
                  alignItems: "center",
                }}
              >
                <Box
                  sx={{
                    fontSize: 96,
                    lineHeight: 1,
                    mb: 2,
                    minHeight: 96,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  {getSourceIcon(
                    modules.find((m) => m.id === storageId)?.backupModuleId ||
                      "",
                  )}
                </Box>
                <TextField
                  select
                  label={t("backupWizard.storage")}
                  value={storageId}
                  onChange={(e) => setStorageId(e.target.value)}
                  fullWidth
                  sx={{ maxWidth: 400 }}
                >
                  {storages.map((s) => (
                    <MenuItem key={s.id} value={s.id}>
                      {s.tag}
                    </MenuItem>
                  ))}
                </TextField>
              </Paper>
            </Box>
            <Stack spacing={2}>
              <TextField
                label={t("backupWizard.tag")}
                value={displayTag}
                onChange={(e) => {
                  setTag(e.target.value);
                  setUserEditedTag(true);
                }}
                fullWidth
              />
              <TextField
                label={t("backupWizard.ignoredPaths")}
                value={ignoredPathsInput}
                onChange={(e) => setIgnoredPathsInput(e.target.value)}
                fullWidth
                multiline
                minRows={4}
                placeholder={t("backupWizard.ignoredPathsPlaceholder")}
              />
              <Stack direction="row" spacing={1} flexWrap="wrap">
                {ignoredPathsInput
                  .split(/\r?\n/)
                  .filter((x) => x.trim() !== "")
                  .map((p) => (
                    <Chip key={p} label={p} size="small" />
                  ))}
                {ignoredPathsInput.trim() === "" && (
                  <Typography variant="caption" color="text.secondary">
                    {t("backupWizard.noIgnoredPaths")}
                  </Typography>
                )}
              </Stack>
            </Stack>
          </Stack>
        </CardContent>
      </Card>
      <Divider />
      <Stack direction="row" spacing={2}>
        <Button variant="outlined" onClick={() => navigate(-1)}>
          {t("common.back")}
        </Button>
        <Button
          variant="contained"
          disabled={!canCreate || state.creating}
          onClick={async () => {
            try {
              setState((s) => ({ ...s, creating: true, createError: null }));
              const payload: CreateBackupRequest = {
                sourceId,
                storageId,
                tag: displayTag,
                ignoredPaths: ignoredPathsInput
                  .split(/\r?\n/)
                  .filter((x) => x.trim() !== ""),
              };
              await backupsApi.create(payload);
              navigate("/backups");
            } catch (e: unknown) {
              const message = e instanceof Error ? e.message : String(e);
              setState((s) => ({
                ...s,
                creating: false,
                createError: message || t("backupWizard.createError"),
              }));
            }
          }}
        >
          {state.creating
            ? t("wizard.creating")
            : t("backupWizard.createBackup")}
        </Button>
      </Stack>
    </Stack>
  );
}
