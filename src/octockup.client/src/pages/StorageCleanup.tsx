import { Refresh } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useStorageCleanupApi } from "../api/storageCleanupApi";
import StorageCleanupHistoryChart from "../components/storage-cleanup/StorageCleanupHistoryChart";
import StorageCleanupRunHistory from "../components/storage-cleanup/StorageCleanupRunHistory";
import StorageCleanupStorageList from "../components/storage-cleanup/StorageCleanupStorageList";
import StorageCleanupSummary from "../components/storage-cleanup/StorageCleanupSummary";
import {
  StorageCleanupStatus,
  type StorageCleanup,
  type StorageCleanupRun,
} from "../types/storageCleanup";
import { getApiErrorMessage } from "../utils/apiError";

const POLL_INTERVAL_MS = 5_000;

export default function StorageCleanupPage() {
  const { t } = useTranslation();
  const storageCleanupApi = useStorageCleanupApi();
  const [cleanups, setCleanups] = useState<StorageCleanup[] | null>(null);
  const [runs, setRuns] = useState<StorageCleanupRun[]>([]);
  const [startingModuleId, setStartingModuleId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [loadedCleanups, loadedRuns] = await Promise.all([
        storageCleanupApi.list(),
        storageCleanupApi.listRuns(),
      ]);
      setCleanups(loadedCleanups);
      setRuns(loadedRuns);
      setError(null);
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(
          getApiErrorMessage(caughtError, t("storageCleanup.loadFailed")),
        );
      }
    }
  }, [storageCleanupApi, t]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!cleanups?.some((item) => item.status === StorageCleanupStatus.Running)) {
      return;
    }

    const interval = window.setInterval(() => {
      void load();
    }, POLL_INTERVAL_MS);
    return () => window.clearInterval(interval);
  }, [cleanups, load]);

  const startCleanup = async (moduleId: string) => {
    setStartingModuleId(moduleId);
    setError(null);
    try {
      await storageCleanupApi.start(moduleId.trim());
      await load();
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(
          getApiErrorMessage(caughtError, t("storageCleanup.startFailed")),
        );
      }
    } finally {
      setStartingModuleId(null);
    }
  };

  return (
    <Box p={2}>
      <Stack spacing={3}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          alignItems={{ xs: "stretch", sm: "center" }}
          justifyContent="space-between"
          spacing={2}
        >
          <Box>
            <Typography variant="h4">{t("storageCleanup.title")}</Typography>
            <Typography variant="body2" color="text.secondary">
              {t("storageCleanup.description")}
            </Typography>
          </Box>
          <Button startIcon={<Refresh />} onClick={() => void load()}>
            {t("common.refresh")}
          </Button>
        </Stack>
        {error && <Alert severity="error">{error}</Alert>}
        {cleanups === null ? (
          <Box display="flex" justifyContent="center" p={4}>
            <CircularProgress />
          </Box>
        ) : (
          <>
            <StorageCleanupSummary cleanups={cleanups} />
            <Box>
              <Typography variant="h5" gutterBottom>
                {t("storageCleanup.storages")}
              </Typography>
              <StorageCleanupStorageList
                cleanups={cleanups}
                runs={runs}
                startingModuleId={startingModuleId}
                onStart={startCleanup}
              />
            </Box>
            {runs.length > 0 && (
              <>
                <StorageCleanupHistoryChart runs={runs} />
                <Box>
                  <Typography variant="h5" gutterBottom>
                    {t("storageCleanup.history.title")}
                  </Typography>
                  <StorageCleanupRunHistory runs={runs} />
                </Box>
              </>
            )}
          </>
        )}
      </Stack>
    </Box>
  );
}
