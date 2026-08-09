import { Refresh } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useState } from "react";
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
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { queryKeys } from "../query/queryKeys";

const POLL_INTERVAL_MS = 5_000;

export default function StorageCleanupPage() {
  const { t } = useTranslation();
  const storageCleanupApi = useStorageCleanupApi();
  const queryClient = useQueryClient();
  const dashboardQuery = useQuery({
    queryKey: queryKeys.storageCleanup,
    queryFn: async () => {
      const [cleanups, runs] = await Promise.all([
        storageCleanupApi.list(),
        storageCleanupApi.listRuns(),
      ]);
      return { cleanups, runs };
    },
    refetchInterval: (query) =>
      query.state.data?.cleanups.some(
        (item) => item.status === StorageCleanupStatus.Running,
      )
        ? POLL_INTERVAL_MS
        : false,
  });
  const cleanups: StorageCleanup[] | null =
    dashboardQuery.data?.cleanups ?? null;
  const runs: StorageCleanupRun[] = dashboardQuery.data?.runs ?? [];
  const [startingModuleId, setStartingModuleId] = useState<string | null>(null);
  const [startError, setStartError] = useState<string | null>(null);
  const loadError = dashboardQuery.error;

  const startCleanup = async (moduleId: string) => {
    setStartingModuleId(moduleId);
    setStartError(null);
    try {
      await storageCleanupApi.start(moduleId.trim());
      await queryClient.invalidateQueries({
        queryKey: queryKeys.storageCleanup,
      });
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setStartError(
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
          <Button
            startIcon={<Refresh />}
            onClick={() => void dashboardQuery.refetch()}
          >
            {t("common.refresh")}
          </Button>
        </Stack>
        {loadError && (
          <Alert severity="error">
            {getApiErrorMessage(loadError, t("storageCleanup.loadFailed"))}
          </Alert>
        )}
        {startError && <Alert severity="error">{startError}</Alert>}
        {cleanups === null && dashboardQuery.isPending ? (
          <Box display="flex" justifyContent="center" p={4}>
            <CircularProgress />
          </Box>
        ) : cleanups !== null ? (
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
        ) : null}
      </Stack>
    </Box>
  );
}
