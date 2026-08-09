import { CleaningServices } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  Stack,
  Typography,
  type ChipProps,
} from "@mui/material";
import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useStorageCleanupApi } from "../../api/storageCleanupApi";
import {
  StorageCleanupStatus,
  type StorageCleanup,
} from "../../types/storageCleanup";
import { getApiErrorMessage } from "../../utils/apiError";
import { formatSize } from "../../utils/formatUtils";

const POLL_INTERVAL_MS = 5_000;

export default function StorageCleanupCard() {
  const { t } = useTranslation();
  const storageCleanupApi = useStorageCleanupApi();
  const [cleanups, setCleanups] = useState<StorageCleanup[] | null>(null);
  const [startingModuleId, setStartingModuleId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadCleanups = useCallback(async () => {
    try {
      const loaded = await storageCleanupApi.list();
      setCleanups(loaded);
      setError(null);
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(getApiErrorMessage(caughtError, t("settings.cleanup.loadFailed")));
      }
    }
  }, [storageCleanupApi, t]);

  useEffect(() => {
    void loadCleanups();
  }, [loadCleanups]);

  useEffect(() => {
    if (!cleanups?.some((cleanup) => cleanup.status === StorageCleanupStatus.Running)) {
      return;
    }

    const interval = window.setInterval(() => {
      void loadCleanups();
    }, POLL_INTERVAL_MS);
    return () => window.clearInterval(interval);
  }, [cleanups, loadCleanups]);

  const startCleanup = async (moduleId: string) => {
    setStartingModuleId(moduleId);
    setError(null);
    try {
      const started = await storageCleanupApi.start(moduleId.trim());
      setCleanups((current) =>
        current?.map((cleanup) =>
          cleanup.moduleId === started.moduleId ? started : cleanup,
        ) ?? [started],
      );
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(getApiErrorMessage(caughtError, t("settings.cleanup.startFailed")));
      }
    } finally {
      setStartingModuleId(null);
    }
  };

  return (
    <Card>
      <CardContent>
        <Stack spacing={2}>
          <Box>
            <Typography variant="h6">{t("settings.cleanup.title")}</Typography>
            <Typography variant="body2" color="text.secondary">
              {t("settings.cleanup.description")}
            </Typography>
          </Box>
          {error && <Alert severity="error">{error}</Alert>}
          {cleanups === null ? (
            <CircularProgress size={24} />
          ) : cleanups.length === 0 ? (
            <Alert severity="info">{t("settings.cleanup.noStorages")}</Alert>
          ) : (
            <Stack divider={<Divider flexItem />} spacing={2}>
              {cleanups.map((cleanup) => (
                <Stack
                  key={cleanup.moduleId}
                  direction={{ xs: "column", md: "row" }}
                  alignItems={{ xs: "stretch", md: "center" }}
                  justifyContent="space-between"
                  spacing={2}
                >
                  <Stack spacing={0.5}>
                    <Stack direction="row" alignItems="center" spacing={1}>
                      <Typography fontWeight={500}>{cleanup.moduleTag}</Typography>
                      <Chip
                        size="small"
                        label={getStatusLabel(cleanup.status, t)}
                        color={getStatusColor(cleanup.status)}
                      />
                    </Stack>
                    <Typography variant="body2" color="text.secondary">
                      {t("settings.cleanup.statistics", {
                        scanned: cleanup.scannedChunks,
                        pending: cleanup.pendingChunks,
                        deleted: cleanup.totalDeletedChunks,
                        reclaimed: formatSize(cleanup.totalReclaimedBytes),
                      })}
                    </Typography>
                    {cleanup.errorMessage && (
                      <Typography variant="body2" color="error">
                        {cleanup.errorMessage}
                      </Typography>
                    )}
                  </Stack>
                  <Button
                    variant="outlined"
                    startIcon={<CleaningServices />}
                    disabled={
                      cleanup.status === StorageCleanupStatus.Running ||
                      startingModuleId === cleanup.moduleId
                    }
                    onClick={() => startCleanup(cleanup.moduleId)}
                  >
                    {cleanup.status === StorageCleanupStatus.Running
                      ? t("settings.cleanup.running")
                      : t("settings.cleanup.start")}
                  </Button>
                </Stack>
              ))}
            </Stack>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}

function getStatusColor(status: StorageCleanupStatus): ChipProps["color"] {
  switch (status) {
    case StorageCleanupStatus.Idle:
      return "default";
    case StorageCleanupStatus.Running:
      return "info";
    case StorageCleanupStatus.Completed:
      return "success";
    case StorageCleanupStatus.Failed:
      return "error";
  }
}

function getStatusLabel(
  status: StorageCleanupStatus,
  t: (key: string) => string,
): string {
  switch (status) {
    case StorageCleanupStatus.Idle:
      return t("settings.cleanup.status.idle");
    case StorageCleanupStatus.Running:
      return t("settings.cleanup.status.running");
    case StorageCleanupStatus.Completed:
      return t("settings.cleanup.status.completed");
    case StorageCleanupStatus.Failed:
      return t("settings.cleanup.status.failed");
  }
}
