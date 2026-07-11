import {
  SnapshotArchiveStatus,
  type SnapshotArchiveJob,
} from "../types/api";

export function isSnapshotArchiveActive(
  job?: SnapshotArchiveJob,
): boolean {
  return (
    job?.status === SnapshotArchiveStatus.Pending ||
    job?.status === SnapshotArchiveStatus.Running
  );
}

export function isSnapshotArchiveTerminal(job: SnapshotArchiveJob): boolean {
  return !isSnapshotArchiveActive(job);
}

export function getSnapshotArchiveProgressPercent(
  job: SnapshotArchiveJob,
): number {
  if (job.totalBytes > 0 && job.processedBytes > 0) {
    return Math.min(100, (job.processedBytes / job.totalBytes) * 100);
  }

  if (job.totalFiles > 0) {
    return Math.min(100, (job.processedFiles / job.totalFiles) * 100);
  }

  return 0;
}
