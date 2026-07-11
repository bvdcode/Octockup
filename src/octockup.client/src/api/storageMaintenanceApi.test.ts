import axios, {
  AxiosError,
  AxiosHeaders,
  type InternalAxiosRequestConfig,
} from "axios";
import { describe, expect, it } from "vitest";
import {
  ModuleDestination,
  StorageCleanupPhase,
  StorageCleanupStatus,
  type StorageCleanupJob,
  type StorageMaintenanceSummary,
} from "../types/api";
import { StorageMaintenanceApiClient } from "./storageMaintenanceApiClient";

describe("StorageMaintenanceApiClient", () => {
  it("uses bounded stats, job polling, start, and cancel endpoints", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    const job = createJob();
    const summary = createSummary(job);
    const axiosInstance = axios.create({
      adapter: async (config) => {
        requests.push(config);
        const data = config.url?.endsWith("/jobs")
          ? [job]
          : config.url?.endsWith("/stats")
            ? summary
            : config.url?.endsWith("/cleanup")
              ? job
              : config.url?.endsWith("/cancel")
                ? null
                : [summary];
        return {
          data,
          status: 200,
          statusText: "OK",
          headers: new AxiosHeaders(),
          config,
        };
      },
    });
    const client = new StorageMaintenanceApiClient(() => axiosInstance);

    await client.list();
    await client.listJobs();
    await client.getStats("storage/id");
    await client.startCleanup("storage/id");
    await client.cancelCleanup("job/id");

    expect(requests.map((request) => [request.method, request.url])).toEqual([
      ["get", "/api/v1/storage-maintenance"],
      ["get", "/api/v1/storage-maintenance/jobs"],
      ["get", "/api/v1/storage-maintenance/storages/storage%2Fid/stats"],
      ["post", "/api/v1/storage-maintenance/storages/storage%2Fid/cleanup"],
      ["post", "/api/v1/storage-maintenance/jobs/job%2Fid/cancel"],
    ]);
  });

  it("preserves authorization failures for the page error state", async () => {
    const axiosInstance = axios.create({
      adapter: async (config) => {
        throw new AxiosError(
          "Forbidden",
          AxiosError.ERR_BAD_RESPONSE,
          config,
          undefined,
          {
            data: { message: "Storage does not belong to this user." },
            status: 403,
            statusText: "Forbidden",
            headers: new AxiosHeaders(),
            config,
          },
        );
      },
    });
    const client = new StorageMaintenanceApiClient(() => axiosInstance);

    await expect(client.startCleanup("other-storage")).rejects.toMatchObject({
      response: { status: 403 },
    });
  });
});

function createSummary(job: StorageCleanupJob): StorageMaintenanceSummary {
  return {
    id: "storage",
    createdAt: "2030-01-01",
    userId: "user",
    tag: "Storage",
    backupModuleId: "provider",
    destination: ModuleDestination.Target,
    indexedObjects: 100,
    activeJob: job,
  };
}

function createJob(): StorageCleanupJob {
  return {
    jobId: "job",
    userId: "user",
    storageId: "storage",
    storageTag: "Storage",
    status: StorageCleanupStatus.Running,
    phase: StorageCleanupPhase.ScanningStorage,
    startedAt: "2030-01-01",
    snapshotFilesScanned: 10,
    referenceCount: 10,
    referencedChunks: 10,
    storageObjectsScanned: 20,
    storageBytesScanned: 30,
    chunkObjectsScanned: 20,
    referencedObjects: 10,
    referencedBytes: 10,
    orphanObjects: 10,
    orphanBytes: 20,
    deletedObjects: 5,
    freedBytes: 10,
    missingObjects: 0,
    missingIndexedObjects: 0,
    failedDeletes: 0,
    skippedObjects: 0,
    uploadedHashRowsDeleted: 0,
  };
}
