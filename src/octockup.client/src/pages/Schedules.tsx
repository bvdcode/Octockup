import {
  Box,
  Card,
  Stack,
  Button,
  Divider,
  Typography,
  CardContent,
  CircularProgress,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { useSchedules } from "../hooks/useSchedules";
import { AddCircleOutline, DeleteSweep } from "@mui/icons-material";
import { ScheduleCard } from "../components/ScheduleCard";

export default function SchedulesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const {
    items,
    scheduleReports,
    state,
    deleteSchedule,
    cancelSchedule,
    resetError,
    cleanupCompletedSchedules,
  } = useSchedules();

  if (state.loading && items.length === 0) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }

  const hasItems = items.length > 0;

  return (
    <Stack spacing={3}>
      <Box display="flex" alignItems="center" justifyContent="space-between">
        <Typography variant="h5">{t("schedules.title")}</Typography>
        <Box display="flex" gap={2}>
          <Button
            startIcon={<DeleteSweep />}
            onClick={cleanupCompletedSchedules}
            disabled={state.cleaningUp || state.loading}
          >
            {t("schedules.cleanup")}
          </Button>
          <Button
            variant="contained"
            startIcon={<AddCircleOutline />}
            onClick={() => navigate("/schedules/new")}
          >
            {t("schedules.newSchedule")}
          </Button>
        </Box>
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
          {items.map((item) => (
            <ScheduleCard
              key={item.id}
              item={item}
              report={scheduleReports[item.id]}
              onDelete={deleteSchedule}
              onCancel={cancelSchedule}
              onResetError={resetError}
              isDeleting={state.deletingId === item.id}
              isCanceling={state.cancelingId === item.id}
              isResetting={state.resettingId === item.id}
            />
          ))}
        </Stack>
      )}
      <Divider />
    </Stack>
  );
}
