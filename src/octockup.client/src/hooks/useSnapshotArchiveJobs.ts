import { useCallback, useEffect, useMemo, useState } from "react";
import { useSnapshotsApi } from "../api/snapshotsApi";
import { useSignalR } from "./useSignalR";
import type { SnapshotArchiveJob } from "../types/api";
import { LatestValueByKeyThrottler } from "../utils/LatestValueByKeyThrottler";
import {
  isSnapshotArchiveActive,
  isSnapshotArchiveTerminal,
} from "../utils/snapshotArchiveUtils";

const progressRenderIntervalMs = 250;
const activePollingIntervalMs = 2000;
const idlePollingIntervalMs = 10000;

interface SnapshotArchiveJobsResult {
  jobsBySnapshot: Record<string, SnapshotArchiveJob>;
  loadFailed: boolean;
  upsertJob: (job: SnapshotArchiveJob) => void;
  reload: () => Promise<void>;
}

export function useSnapshotArchiveJobs(
  backupId?: string,
): SnapshotArchiveJobsResult {
  const api = useSnapshotsApi();
  const { connection, isConnected } = useSignalR("/api/v1/event-hub");
  const [jobsBySnapshot, setJobsBySnapshot] = useState<
    Record<string, SnapshotArchiveJob>
  >({});
  const [loadFailed, setLoadFailed] = useState(false);

  const upsertJob = useCallback((job: SnapshotArchiveJob) => {
    setJobsBySnapshot((current) => ({
      ...current,
      [job.snapshotId]: job,
    }));
  }, []);

  const reload = useCallback(async () => {
    if (!backupId) return;
    try {
      const jobs = await api.listArchiveJobs(backupId);
      setJobsBySnapshot(
        Object.fromEntries(jobs.map((job) => [job.snapshotId, job])),
      );
      setLoadFailed(false);
    } catch {
      setLoadFailed(true);
    }
  }, [api, backupId]);

  useEffect(() => {
    const timer = window.setTimeout(() => void reload(), 0);
    return () => window.clearTimeout(timer);
  }, [isConnected, reload]);

  useEffect(() => {
    if (!connection || !isConnected) return;

    const throttler = new LatestValueByKeyThrottler<
      string,
      SnapshotArchiveJob
    >(upsertJob, progressRenderIntervalMs);
    const handler = (job: SnapshotArchiveJob) => {
      throttler.push(job.jobId, job, isSnapshotArchiveTerminal(job));
    };
    connection.on("SnapshotArchiveProgress", handler);

    return () => {
      connection.off("SnapshotArchiveProgress", handler);
      throttler.dispose();
    };
  }, [connection, isConnected, upsertJob]);

  const hasActiveJobs = useMemo(
    () => Object.values(jobsBySnapshot).some(isSnapshotArchiveActive),
    [jobsBySnapshot],
  );

  useEffect(() => {
    const interval = window.setInterval(
      () => void reload(),
      hasActiveJobs ? activePollingIntervalMs : idlePollingIntervalMs,
    );
    return () => window.clearInterval(interval);
  }, [hasActiveJobs, reload]);

  return { jobsBySnapshot, loadFailed, upsertJob, reload };
}
