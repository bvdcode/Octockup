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
  ButtonGroup,
  Tooltip,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { useBackupsApi } from "../api/backupsApi";
import { useSchedulesApi } from "../api/schedulesApi";
import type { BackupItem, CreateScheduleRequest } from "../types/api";
import { useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "../query/queryKeys";

interface State { loading: boolean; error: string | null; creating: boolean; createError: string | null; }

export default function ScheduleWizard() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const backupsApi = useBackupsApi();
  const schedulesApi = useSchedulesApi();
  const queryClient = useQueryClient();

  const [state, setState] = useState<State>({ loading: true, error: null, creating: false, createError: null });
  const [backups, setBackups] = useState<BackupItem[]>([]);
  const [backupId, setBackupId] = useState<string>("");
  const [startAt, setStartAt] = useState<string>(() => {
    const now = new Date();
    now.setSeconds(0, 0);
    // datetime-local expects format: YYYY-MM-DDTHH:mm (local time, no Z)
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  });
  const [intervalMinutes, setIntervalMinutes] = useState<string>("");

  useEffect(() => {
    let active = true;
    backupsApi.list()
      .then(data => { if (!active) return; setBackups(data); setState(s => ({ ...s, loading: false })); })
      .catch(e => { if (!active) return; setState({ loading: false, error: e?.message || "Failed to load backups", creating: false, createError: null }); });
    return () => { active = false; };
  }, [backupsApi]);

  const canCreate = useMemo(() => !!backupId && !!startAt, [backupId, startAt]);

  if (state.loading) {
    return <Box display="flex" justifyContent="center" p={4}><CircularProgress /></Box>;
  }
  if (state.error) {
    return <Box p={2}><Alert severity="error">{state.error}</Alert></Box>;
  }

  return (
    <Stack spacing={3}>
      <Typography variant="h5">{t("scheduleWizard.title")}</Typography>
      {state.createError && <Alert severity="error">{state.createError}</Alert>}
      <Card>
        <CardContent>
          <Stack spacing={2}>
            <TextField
              select
              label={t("scheduleWizard.backup")}
              value={backupId}
              onChange={e => setBackupId(e.target.value)}
              fullWidth
            >
              {backups.map(b => (
                <MenuItem key={b.id} value={b.id}>{`${b.tag} — ${b.source.tag} → ${b.storage.tag}`}</MenuItem>
              ))}
            </TextField>
            <TextField
              type="datetime-local"
              label={t("scheduleWizard.startAt")}
              value={startAt}
              onChange={e => setStartAt(e.target.value)}
              fullWidth
              InputLabelProps={{ shrink: true }}
            />
            <Box display="flex" gap={2} alignItems="center">
              <TextField
                type="number"
                label={t("scheduleWizard.intervalMinutes")}
                value={intervalMinutes}
                onChange={e => setIntervalMinutes(e.target.value)}
                sx={{ flex: 1 }}
                inputProps={{ min: 0 }}
              />
              <ButtonGroup variant="outlined" size="small">
                <Tooltip title={t("scheduleWizard.interval1h")}>
                  <Button onClick={() => setIntervalMinutes("60")}>1h</Button>
                </Tooltip>
                <Tooltip title={t("scheduleWizard.interval1d")}>
                  <Button onClick={() => setIntervalMinutes(String(60 * 24))}>1d</Button>
                </Tooltip>
                <Tooltip title={t("scheduleWizard.interval1w")}>
                  <Button onClick={() => setIntervalMinutes(String(60 * 24 * 7))}>1w</Button>
                </Tooltip>
                <Tooltip title={t("scheduleWizard.interval1m")}>
                  <Button onClick={() => setIntervalMinutes(String(60 * 24 * 30))}>1m</Button>
                </Tooltip>
              </ButtonGroup>
            </Box>
          </Stack>
        </CardContent>
      </Card>
      <Divider />
      <Stack direction="row" spacing={2}>
        <Button variant="outlined" onClick={() => navigate(-1)}>{t("common.back")}</Button>
        <Button
          variant="contained"
          disabled={!canCreate || state.creating}
          onClick={async () => {
            try {
              setState(s => ({ ...s, creating: true, createError: null }));
              // datetime-local value is in local time format (YYYY-MM-DDTHH:mm)
              // Create a Date object which will interpret it as local time
              const localDate = new Date(startAt);
              const payload: CreateScheduleRequest = {
                backupId,
                startAt: localDate.toISOString(), // Convert to UTC ISO string for server
                intervalMinutes: intervalMinutes ? parseInt(intervalMinutes, 10) : undefined,
              };
              await schedulesApi.create(payload);
              await Promise.all([
                queryClient.invalidateQueries({
                  queryKey: queryKeys.schedules,
                  refetchType: "all",
                }),
                queryClient.invalidateQueries({
                  queryKey: queryKeys.backups,
                  refetchType: "all",
                }),
              ]);
              navigate("/schedules");
            } catch (e: unknown) {
              const message = e instanceof Error ? e.message : String(e);
              setState(s => ({ ...s, creating: false, createError: message || t("scheduleWizard.createError") }));
            }
          }}
        >
          {state.creating ? t("wizard.creating") : t("scheduleWizard.createSchedule")}
        </Button>
      </Stack>
    </Stack>
  );
}
