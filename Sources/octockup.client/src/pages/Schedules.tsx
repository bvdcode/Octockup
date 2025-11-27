import {
  Box,
  Card,
  Stack,
  Alert,
  Chip,
  Divider,
  Typography,
  CardContent,
  CircularProgress,
  Button,
} from "@mui/material";
import { AddCircleOutline } from "@mui/icons-material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { getSourceIcon } from "../constants/sourceIcons";
import { useSchedulesApi } from "../api/schedulesApi";
import type { ScheduleItem } from "../types/api";
import { BackupStatus } from "../types/api";

interface State { loading: boolean; error: string | null; }

function statusColor(status: BackupStatus): "default" | "success" | "error" | "warning" | "info" {
  switch (status) {
    case BackupStatus.Completed: return "success";
    case BackupStatus.Running: return "info";
    case BackupStatus.Failed: return "error";
    case BackupStatus.Created: default: return "default";
  }
}

export default function SchedulesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useSchedulesApi();
  const [state, setState] = useState<State>({ loading: true, error: null });
  const [items, setItems] = useState<ScheduleItem[]>([]);

  useEffect(() => {
    let active = true;
    api.list()
      .then(data => { if (!active) return; setItems(data); setState({ loading: false, error: null }); })
      .catch(e => { if (!active) return; setState({ loading: false, error: e?.message || "Failed to load schedules" }); });
    return () => { active = false; };
  }, [api]);

  if (state.loading) {
    return <Box display="flex" justifyContent="center" p={4}><CircularProgress /></Box>;
  }
  if (state.error) {
    return <Box p={2}><Alert severity="error">{state.error}</Alert></Box>;
  }

  const hasItems = items.length > 0;

  return (
    <Stack spacing={3}>
      <Box display="flex" alignItems="center" justifyContent="space-between">
        <Typography variant="h5">{t("schedules.title")}</Typography>
        <Button variant="contained" startIcon={<AddCircleOutline />} onClick={() => navigate("/schedules/new")}>{t("schedules.newSchedule")}</Button>
      </Box>
      {!hasItems ? (
        <Card variant="outlined"><CardContent><Typography color="text.secondary">{t("schedules.noSchedules")}</Typography></CardContent></Card>
      ) : (
        <Stack direction="row" spacing={2} flexWrap="wrap">
          {items.map(it => (
            <Card key={it.id} sx={{ width: 260, height: 170, flex: "0 0 260px", display: "flex", position: "relative" }} data-animatable={it.status === BackupStatus.Running ? "true" : undefined}>
              <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, justifyContent: "space-between", height: "100%", p: 2, width: "100%" }}>
                <Box display="flex" alignItems="center" gap={1} width="100%" justifyContent="space-between">
                  <Box fontSize={24}>{getSourceIcon(it.backup.source.backupModuleId)}</Box>
                  <Typography variant="subtitle2" noWrap title={it.backup.source.tag} sx={{ maxWidth: 120, textAlign: "center" }}>{it.backup.source.tag}</Typography>
                  <Box fontSize={24}>{getSourceIcon(it.backup.storage.backupModuleId)}</Box>
                </Box>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>{it.backup.tag}</Typography>
                <Chip size="small" label={t(`schedules.status.${BackupStatus[it.status].toLowerCase()}`)} color={statusColor(it.status)} />
                <Typography variant="caption" sx={{ color: "text.secondary", textAlign: "center" }}>
                  {new Date(it.startAt).toLocaleString()}
                  {it.interval ? (() => { const parts = String(it.interval).split(":"); const minutes = parts.length >= 2 ? parseInt(parts[0]) * 60 + parseInt(parts[1]) : 0; return ` • ${t("schedules.everyMinutes", { count: minutes })}`; })() : ""}
                </Typography>
              </CardContent>
            </Card>
          ))}
        </Stack>
      )}
      <Divider />
    </Stack>
  );
}
