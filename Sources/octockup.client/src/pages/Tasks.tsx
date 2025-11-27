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
import { useTasksApi } from "../api/tasksApi";
import type { TaskItem } from "../types/api";
import { TaskStatus } from "../types/api";

interface State {
  loading: boolean;
  error: string | null;
}

function statusColor(status: TaskStatus): "default" | "success" | "error" | "warning" | "info" {
  switch (status) {
    case TaskStatus.Completed:
      return "success";
    case TaskStatus.Running:
      return "info";
    case TaskStatus.Failed:
      return "error";
    case TaskStatus.Created:
    default:
      return "default";
  }
}

export default function TasksPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useTasksApi();
  const [state, setState] = useState<State>({ loading: true, error: null });
  const [tasks, setTasks] = useState<TaskItem[]>([]);

  useEffect(() => {
    let active = true;
    api
      .list()
      .then((data) => {
        if (!active) return;
        setTasks(data);
        setState({ loading: false, error: null });
      })
      .catch((e) => {
        if (!active) return;
        setState({ loading: false, error: e?.message || "Failed to load tasks" });
      });
    return () => {
      active = false;
    };
  }, [api]);

  const hasTasks = tasks.length > 0;

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
        <Typography variant="h5">{t("tasks.title")}</Typography>
        <Button
          variant="contained"
          startIcon={<AddCircleOutline />}
          onClick={() => navigate("/tasks/new")}
        >
          {t("tasks.newTask")}
        </Button>
      </Box>
      {!hasTasks ? (
        <Card variant="outlined">
          <CardContent>
            <Typography color="text.secondary">{t("tasks.noTasks")}</Typography>
          </CardContent>
        </Card>
      ) : (
        <Stack direction="row" spacing={2} flexWrap="wrap">
          {tasks.map((task) => (
            <Card
              key={task.id}
              sx={{
                width: 260,
                height: 160,
                flex: "0 0 260px",
                display: "flex",
                alignItems: "stretch",
                justifyContent: "center",
                position: "relative",
              }}
              data-animatable={task.status === TaskStatus.Running ? "true" : undefined}
            >
              <CardContent
                sx={{
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  gap: 1,
                  justifyContent: "space-between",
                  height: "100%",
                  p: 2,
                  width: "100%",
                }}
              >
                <Box display="flex" alignItems="center" gap={1} width="100%" justifyContent="space-between">
                  <Box fontSize={24}>{getSourceIcon(task.sourceProviderId)}</Box>
                  <Typography variant="subtitle2" noWrap title={task.sourceTag} sx={{ maxWidth: 120, textAlign: "center" }}>
                    {task.sourceTag}
                  </Typography>
                  <Box fontSize={24}>{getSourceIcon(task.storageProviderId)}</Box>
                </Box>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  {task.backupTag}
                </Typography>
                <Chip
                  size="small"
                  label={t(`tasks.status.${TaskStatus[task.status].toLowerCase()}`)}
                  color={statusColor(task.status)}
                />
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  {new Date(task.startAt).toLocaleString()}
                  {task.interval
                    ? (() => {
                        const parts = String(task.interval).split(":");
                        const minutes = parts.length >= 2 ? parseInt(parts[0]) * 60 + parseInt(parts[1]) : 0;
                        return ` • ${t("tasks.everyMinutes", { count: minutes })}`;
                      })()
                    : ""}
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
