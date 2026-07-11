import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useSnapshotsApi } from "../api/snapshotsApi";
import type { SnapshotArchiveJob } from "../types/api";
import {
  createTicketDownloadUrl,
  openPreparedTicketDownload,
} from "../utils/downloadUtils";

interface SnapshotArchiveActionsResult {
  downloadingId: string | null;
  copyingId: string | null;
  cancelingJobId: string | null;
  error: string | null;
  success: string | null;
  download: (snapshotId: string) => Promise<void>;
  copyLink: (snapshotId: string) => Promise<void>;
  cancel: (job: SnapshotArchiveJob) => Promise<void>;
  clearSuccess: () => void;
}

export function useSnapshotArchiveActions(
  upsertJob: (job: SnapshotArchiveJob) => void,
  reloadJobs: () => Promise<void>,
): SnapshotArchiveActionsResult {
  const { t } = useTranslation();
  const api = useSnapshotsApi();
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [copyingId, setCopyingId] = useState<string | null>(null);
  const [cancelingJobId, setCancelingJobId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const download = async (snapshotId: string) => {
    setDownloadingId(snapshotId);
    setError(null);
    try {
      await openPreparedTicketDownload(async () => {
        const job = await api.startArchiveJob(snapshotId);
        upsertJob(job);
        const ticket = await api.createArchiveJobDownloadTicket(job.jobId);
        return {
          path: archiveDownloadPath(job.jobId),
          ticket,
        };
      });
    } catch {
      setError(t("snapshots.downloadFailed"));
    } finally {
      setDownloadingId(null);
    }
  };

  const copyLink = async (snapshotId: string) => {
    setCopyingId(snapshotId);
    setError(null);
    try {
      const job = await api.startArchiveJob(snapshotId);
      upsertJob(job);
      const ticket = await api.createArchiveJobDownloadTicket(job.jobId);
      const url = createTicketDownloadUrl(
        archiveDownloadPath(job.jobId),
        ticket.ticket,
      );
      await navigator.clipboard.writeText(url);
      setSuccess(t("snapshots.linkCopied"));
    } catch {
      setError(t("snapshots.linkCopyFailed"));
    } finally {
      setCopyingId(null);
    }
  };

  const cancel = async (job: SnapshotArchiveJob) => {
    setCancelingJobId(job.jobId);
    setError(null);
    try {
      await api.cancelArchiveJob(job.jobId);
      await reloadJobs();
    } catch {
      setError(t("snapshots.archive.cancelFailed"));
    } finally {
      setCancelingJobId(null);
    }
  };

  return {
    downloadingId,
    copyingId,
    cancelingJobId,
    error,
    success,
    download,
    copyLink,
    cancel,
    clearSuccess: () => setSuccess(null),
  };
}

function archiveDownloadPath(jobId: string): string {
  return `/api/v1/snapshot-archive-jobs/${encodeURIComponent(jobId)}/download`;
}
