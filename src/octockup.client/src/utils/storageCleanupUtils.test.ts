import { describe, expect, it } from "vitest";
import {
  ModuleDestination,
  StorageCleanupPhase,
  StorageCleanupStatus,
  type StorageCleanupJob,
  type StorageMaintenanceSummary,
} from "../types/api";
import {
  isStorageCleanupActive,
  selectStorageCleanupDisplayJob,
} from "./storageCleanupUtils";

describe("storageCleanupUtils", () => {
  it("treats only pending and running jobs as active", () => {
    expect(isStorageCleanupActive(createJob(StorageCleanupStatus.Pending))).toBe(true);
    expect(isStorageCleanupActive(createJob(StorageCleanupStatus.Running))).toBe(true);
    expect(isStorageCleanupActive(createJob(StorageCleanupStatus.Completed))).toBe(false);
    expect(isStorageCleanupActive(createJob(StorageCleanupStatus.Failed))).toBe(false);
    expect(isStorageCleanupActive(createJob(StorageCleanupStatus.Canceled))).toBe(false);
    expect(isStorageCleanupActive()).toBe(false);
  });

  it("prefers refreshed job state over stale summary state", () => {
    const staleJob = createJob(StorageCleanupStatus.Running);
    const completedJob = createJob(StorageCleanupStatus.Completed);
    const storage: StorageMaintenanceSummary = {
      id: "storage",
      createdAt: "2030-01-01",
      userId: "user",
      tag: "Storage",
      backupModuleId: "provider",
      destination: ModuleDestination.Target,
      activeJob: staleJob,
    };

    expect(
      selectStorageCleanupDisplayJob(storage, { storage: completedJob }),
    ).toBe(completedJob);
  });
});

function createJob(status: StorageCleanupStatus): StorageCleanupJob {
  return {
    jobId: "job",
    userId: "user",
    storageId: "storage",
    storageTag: "Storage",
    status,
    phase: StorageCleanupPhase.ScanningStorage,
    startedAt: "2030-01-01",
    snapshotFilesScanned: 0,
    referenceCount: 0,
    referencedChunks: 0,
    storageObjectsScanned: 0,
    storageBytesScanned: 0,
    chunkObjectsScanned: 0,
    referencedObjects: 0,
    referencedBytes: 0,
    orphanObjects: 0,
    orphanBytes: 0,
    deletedObjects: 0,
    freedBytes: 0,
    missingObjects: 0,
    missingIndexedObjects: 0,
    failedDeletes: 0,
    skippedObjects: 0,
    uploadedHashRowsDeleted: 0,
  };
}
