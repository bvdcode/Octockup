import {
  StorageCleanupStatus,
  type StorageCleanupJob,
  type StorageMaintenanceSummary,
} from "../types/api";

export function isStorageCleanupActive(job?: StorageCleanupJob): boolean {
  return (
    job?.status === StorageCleanupStatus.Pending ||
    job?.status === StorageCleanupStatus.Running
  );
}

export function selectStorageCleanupDisplayJob(
  storage: StorageMaintenanceSummary,
  jobs: Record<string, StorageCleanupJob>,
): StorageCleanupJob | undefined {
  return jobs[storage.id] ?? storage.activeJob ?? storage.lastJob ?? undefined;
}
