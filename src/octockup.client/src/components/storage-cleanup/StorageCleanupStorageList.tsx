import { CleaningServices } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  LinearProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import {
  StorageCleanupStatus,
  type StorageCleanup,
  type StorageCleanupRun,
} from "../../types/storageCleanup";
import {
  formatStorageCleanupDuration,
  getRunDurationSeconds,
  getStorageCleanupStatusColor,
  getStorageCleanupStatusKey,
} from "./storageCleanupPresentation";

interface StorageCleanupStorageListProps {
  cleanups: StorageCleanup[];
  runs: StorageCleanupRun[];
  startingModuleId: string | null;
  onStart: (moduleId: string) => Promise<void>;
}

export default function StorageCleanupStorageList({
  cleanups,
  runs,
  startingModuleId,
  onStart,
}: StorageCleanupStorageListProps) {
  const { t, i18n } = useTranslation();

  if (cleanups.length === 0) {
    return <Alert severity="info">{t("storageCleanup.noStorages")}</Alert>;
  }

  return (
    <Stack spacing={2}>
      {cleanups.map((cleanup) => {
        const latestRun = runs.find(
          (run) => run.moduleId === cleanup.moduleId,
        );
        const currentRun = runs.find(
          (run) =>
            run.moduleId === cleanup.moduleId &&
            run.status === StorageCleanupStatus.Running,
        );
        const displayedRun = currentRun ?? latestRun;
        const duration = displayedRun
          ? getRunDurationSeconds(displayedRun.startedAt, displayedRun.completedAt)
          : 0;
        const scanRate =
          displayedRun && duration > 0
            ? displayedRun.scannedChunks / duration
            : 0;

        return (
          <Card key={cleanup.moduleId} variant="outlined">
            {cleanup.status === StorageCleanupStatus.Running && (
              <LinearProgress />
            )}
            <CardContent>
              <Stack spacing={2}>
                <Stack
                  direction={{ xs: "column", sm: "row" }}
                  alignItems={{ xs: "stretch", sm: "center" }}
                  justifyContent="space-between"
                  spacing={2}
                >
                  <Stack direction="row" alignItems="center" spacing={1}>
                    <Typography variant="h6">{cleanup.moduleTag}</Typography>
                    <Chip
                      size="small"
                      label={t(getStorageCleanupStatusKey(cleanup.status))}
                      color={getStorageCleanupStatusColor(cleanup.status)}
                    />
                  </Stack>
                  <Button
                    variant="outlined"
                    startIcon={<CleaningServices />}
                    disabled={
                      cleanup.status === StorageCleanupStatus.Running ||
                      startingModuleId === cleanup.moduleId
                    }
                    onClick={() => onStart(cleanup.moduleId)}
                  >
                    {cleanup.status === StorageCleanupStatus.Running
                      ? t("storageCleanup.running")
                      : t("storageCleanup.start")}
                  </Button>
                </Stack>
                <Box
                  display="grid"
                  gridTemplateColumns={{
                    xs: "repeat(2, 1fr)",
                    md: "repeat(4, 1fr)",
                  }}
                  gap={2}
                >
                  <Metric
                    label={t("storageCleanup.metrics.scanned")}
                    value={cleanup.scannedChunks.toLocaleString(
                      i18n.resolvedLanguage,
                    )}
                  />
                  <Metric
                    label={t("storageCleanup.metrics.pending")}
                    value={cleanup.pendingChunks.toLocaleString(
                      i18n.resolvedLanguage,
                    )}
                  />
                  <Metric
                    label={t("storageCleanup.metrics.scanRate")}
                    value={t("storageCleanup.chunksPerSecond", {
                      count: Number(scanRate.toFixed(1)),
                    })}
                  />
                  <Metric
                    label={t("storageCleanup.metrics.duration")}
                    value={
                      displayedRun
                        ? formatStorageCleanupDuration(duration, t)
                        : t("common.never")
                    }
                  />
                </Box>
                {cleanup.errorMessage && (
                  <Alert severity="error">{cleanup.errorMessage}</Alert>
                )}
              </Stack>
            </CardContent>
          </Card>
        );
      })}
    </Stack>
  );
}

interface MetricProps {
  label: string;
  value: string;
}

function Metric({ label, value }: MetricProps) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="body1" fontWeight={500}>
        {value}
      </Typography>
    </Box>
  );
}
