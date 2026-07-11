import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Snackbar,
  Stack,
  Typography,
  LinearProgress,
} from "@mui/material";
import { isAxiosError } from "axios";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useSignalR } from "../hooks/useSignalR";
import { useStorageMaintenanceApi } from "../api/storageMaintenanceApi";
import { StorageMaintenanceCard } from "../components/storageMaintenance/StorageMaintenanceCard";
import {
  type StorageCleanupJob,
  type StorageMaintenanceSummary,
} from "../types/api";
import { LatestValueByKeyThrottler } from "../utils/LatestValueByKeyThrottler";
import {
  isStorageCleanupActive,
  selectStorageCleanupDisplayJob,
} from "../utils/storageCleanupUtils";

const progressRenderIntervalMs = 250;

interface SnackbarState {
  message: string;
  severity: "success" | "error";
}

interface ApiErrorResponse {
  message?: string;
}

export default function StorageMaintenancePage() {
  const { t } = useTranslation();
  const api = useStorageMaintenanceApi();
  const { connection, isConnected } = useSignalR("/api/v1/event-hub");
  const [loading, setLoading] = useState(true);
  const [summaries, setSummaries] = useState<StorageMaintenanceSummary[]>([]);
  const [jobs, setJobs] = useState<Record<string, StorageCleanupJob>>({});
  const [statsLoadingIds, setStatsLoadingIds] = useState<Record<string, boolean>>(
    {},
  );
  const [startingId, setStartingId] = useState<string | null>(null);
  const [cancelingJobId, setCancelingJobId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [snackbar, setSnackbar] = useState<SnackbarState | null>(null);

  const upsertJob = useCallback((job: StorageCleanupJob) => {
    setJobs((prev) => ({
      ...prev,
      [job.storageId]: job,
    }));
  }, []);

  const loadStorageStats = useCallback(
    async (storageIds: string[]) => {
      for (const storageId of storageIds) {
        setStatsLoadingIds((prev) => ({ ...prev, [storageId]: true }));
        try {
          const stats = await api.getStats(storageId);
          setSummaries((prev) =>
            prev.map((item) => (item.id === storageId ? stats : item)),
          );
          const job = stats.activeJob ?? stats.lastJob;
          if (job) {
            upsertJob(job);
          }
        } catch {
          setError(t("storageMaintenance.statsLoadFailed"));
        } finally {
          setStatsLoadingIds((prev) => ({ ...prev, [storageId]: false }));
        }
      }
    },
    [api, t, upsertJob],
  );

  const reloadSummaries = useCallback(async () => {
    const data = await api.list();
    setSummaries(data);
    setError(null);
    setJobs((prev) => {
      const next = { ...prev };
      data.forEach((storage) => {
        const job = storage.activeJob ?? storage.lastJob;
        if (job) {
          next[storage.id] = job;
        }
      });
      return next;
    });
    void loadStorageStats(data.map((storage) => storage.id));
  }, [api, loadStorageStats]);

  const reloadJobs = useCallback(async () => {
    const data = await api.listJobs();
    setJobs((prev) => {
      const next = { ...prev };
      data.forEach((job) => {
        next[job.storageId] = job;
      });
      return next;
    });
  }, [api]);

  useEffect(() => {
    if (!isConnected) return;

    void Promise.all([reloadSummaries(), reloadJobs()]).catch(() => {
      setError(t("storageMaintenance.loadFailed"));
    });
  }, [isConnected, reloadJobs, reloadSummaries, t]);

  useEffect(() => {
    let active = true;
    reloadSummaries()
      .catch((e) => {
        if (!active) return;
        setError(e?.message || t("storageMaintenance.loadFailed"));
      })
      .finally(() => {
        if (active) {
          setLoading(false);
        }
      });
    return () => {
      active = false;
    };
  }, [reloadSummaries, t]);

  useEffect(() => {
    if (!connection || !isConnected) return;

    const applyJob = (job: StorageCleanupJob) => {
      upsertJob(job);
      if (!isStorageCleanupActive(job)) {
        setTimeout(() => {
          void reloadSummaries().catch(() => {
            setError(t("storageMaintenance.loadFailed"));
          });
        }, 750);
      }
    };
    const throttler = new LatestValueByKeyThrottler<string, StorageCleanupJob>(
      applyJob,
      progressRenderIntervalMs,
    );
    const handler = (job: StorageCleanupJob) => {
      throttler.push(job.jobId, job, !isStorageCleanupActive(job));
    };

    connection.on("StorageCleanupProgress", handler);

    return () => {
      connection.off("StorageCleanupProgress", handler);
      throttler.dispose();
    };
  }, [connection, isConnected, reloadSummaries, t, upsertJob]);

  const hasActiveJobs = useMemo(
    () => Object.values(jobs).some((job) => isStorageCleanupActive(job)),
    [jobs],
  );

  useEffect(() => {
    const interval = window.setInterval(
      () => {
        void reloadJobs().catch(() => undefined);
      },
      hasActiveJobs ? 2000 : 10000,
    );

    return () => window.clearInterval(interval);
  }, [hasActiveJobs, reloadJobs]);

  const startCleanup = async (storage: StorageMaintenanceSummary) => {
    const result = await confirm({
      title: t("storageMaintenance.confirmTitle"),
      description: t("storageMaintenance.confirmText", {
        storage: storage.tag,
      }),
      confirmationText: t("storageMaintenance.start"),
      cancellationText: t("common.cancel"),
      confirmationButtonProps: { color: "warning" },
    });

    if (!result.confirmed) {
      return;
    }

    setStartingId(storage.id);
    try {
      const job = await api.startCleanup(storage.id);
      upsertJob(job);
      setSnackbar({
        severity: "success",
        message: t("storageMaintenance.started"),
      });
    } catch (e) {
      const message = isAxiosError<ApiErrorResponse>(e)
        ? e.response?.data?.message || t("storageMaintenance.startFailed")
        : t("storageMaintenance.startFailed");
      setSnackbar({ severity: "error", message });
    } finally {
      setStartingId(null);
    }
  };

  const cancelCleanup = async (jobId: string) => {
    setCancelingJobId(jobId);
    try {
      await api.cancelCleanup(jobId);
      await reloadJobs();
    } finally {
      setCancelingJobId(null);
    }
  };

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h5">{t("storageMaintenance.title")}</Typography>
        <Typography variant="body2" color="text.secondary">
          {t("storageMaintenance.subtitle")}
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {loading && <LinearProgress />}

      {!loading && summaries.length === 0 && (
        <Alert severity="info">{t("storageMaintenance.noStorages")}</Alert>
      )}

      <Stack spacing={2}>
        {summaries.map((storage) => {
          const job = selectStorageCleanupDisplayJob(storage, jobs);
          return (
            <StorageMaintenanceCard
              key={storage.id}
              storage={storage}
              job={job}
              starting={startingId === storage.id}
              canceling={cancelingJobId === job?.jobId}
              statsLoading={!!statsLoadingIds[storage.id]}
              onStart={() => startCleanup(storage)}
              onCancel={cancelCleanup}
            />
          );
        })}
      </Stack>

      <Snackbar
        open={snackbar !== null}
        autoHideDuration={6000}
        onClose={() => setSnackbar(null)}
      >
        <Alert
          severity={snackbar?.severity ?? "success"}
          onClose={() => setSnackbar(null)}
        >
          {snackbar?.message}
        </Alert>
      </Snackbar>
    </Stack>
  );
}
