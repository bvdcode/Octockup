import {
  Box,
  Stack,
  Alert,
  Button,
  Divider,
  Typography,
  CircularProgress,
  MenuItem,
  TextField,
  Card,
  CardContent,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { useBackupsApi } from "../api/backupsApi";
import { useTasksApi } from "../api/tasksApi";
import type { BackupSummary, CreateTaskRequest } from "../types/api";

interface State {
  loading: boolean;
  error: string | null;
  creating: boolean;
  createError: string | null;
}

export default function TaskWizard() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const backupsApi = useBackupsApi();
  const tasksApi = useTasksApi();

  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    creating: false,
    createError: null,
  });
  const [backups, setBackups] = useState<BackupSummary[]>([]);

  const [backupId, setBackupId] = useState<string>("");
  const [startAt, setStartAt] = useState<string>("");
  const [intervalMinutes, setIntervalMinutes] = useState<string>("");

  useEffect(() => {
    let active = true;
    backupsApi
      .list()
      .then((data) => {
        if (!active) return;
        setBackups(data);
        setState((s) => ({ ...s, loading: false }));
      })
      .catch((e) => {
        if (!active) return;
        setState({ loading: false, error: e?.message || "Failed to load backups", creating: false, createError: null });
      });
    return () => {
      active = false;
    };
  }, [backupsApi]);

  const canCreate = useMemo(() => {
    return !!backupId && !!startAt;
  }, [backupId, startAt]);

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
      <Typography variant="h5">{t("taskWizard.title")}</Typography>
      {state.createError && (
        <Alert severity="error">{state.createError}</Alert>
      )}
      <Card variant="outlined">
        <CardContent>
          <Stack spacing={2}>
            <TextField
              select
              label={t("taskWizard.backup")}
              value={backupId}
              onChange={(e) => setBackupId(e.target.value)}
              fullWidth
            >
              {backups.map((b) => (
                <MenuItem key={b.id} value={b.id}>{`${b.tag} — ${b.sourceTag} → ${b.storageTag}`}</MenuItem>
              ))}
            </TextField>
            <TextField
              type="datetime-local"
              label={t("taskWizard.startAt")}
              value={startAt}
              onChange={(e) => setStartAt(e.target.value)}
              fullWidth
              InputLabelProps={{ shrink: true }}
            />
            <TextField
              type="number"
              label={t("taskWizard.intervalMinutes")}
              value={intervalMinutes}
              onChange={(e) => setIntervalMinutes(e.target.value)}
              fullWidth
              inputProps={{ min: 0 }}
            />
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
              const payload: CreateTaskRequest = {
                backupId,
                startAt: new Date(startAt).toISOString(),
                intervalMinutes: intervalMinutes ? parseInt(intervalMinutes, 10) : undefined,
              };
              await tasksApi.create(payload);
              navigate("/tasks");
            } catch (e: unknown) {
              const message = e instanceof Error ? e.message : String(e);
              setState((s) => ({ ...s, creating: false, createError: message || t("taskWizard.createError") }));
            }
          }}
        >
          {state.creating ? t("wizard.creating") : t("taskWizard.createTask")}
        </Button>
      </Stack>
    </Stack>
  );
}
