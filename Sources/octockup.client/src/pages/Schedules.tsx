import {
  Box,
  Card,
  Stack,
  Alert,
  Button,
  Divider,
  Typography,
  CardContent,
  CircularProgress,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { useSchedules } from "../hooks/useSchedules";
import { AddCircleOutline } from "@mui/icons-material";
import { ScheduleCard } from "../components/ScheduleCard";

export default function SchedulesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { items, scheduleReports, state, deleteSchedule, cancelSchedule } =
    useSchedules();

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
          {items.map((item) => (
            <ScheduleCard
              key={item.id}
              item={item}
              report={scheduleReports[item.id]}
              onDelete={deleteSchedule}
              onCancel={cancelSchedule}
              isDeleting={state.deletingId === item.id}
              isCanceling={state.cancelingId === item.id}
            />
          ))}
        </Stack>
      )}
      <Divider />
    </Stack>
  );
}
