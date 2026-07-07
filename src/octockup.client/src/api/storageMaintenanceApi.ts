import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type {
  StorageCleanupJob,
  StorageMaintenanceSummary,
} from "../types/api";

class StorageMaintenanceApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<StorageMaintenanceSummary[]> {
    const result = await this.axios().get<StorageMaintenanceSummary[]>(
      "/api/v1/storage-maintenance",
    );
    return result.data;
  }

  async listJobs(): Promise<StorageCleanupJob[]> {
    const result = await this.axios().get<StorageCleanupJob[]>(
      "/api/v1/storage-maintenance/jobs",
    );
    return result.data;
  }

  async getStats(storageId: string): Promise<StorageMaintenanceSummary> {
    const result = await this.axios().get<StorageMaintenanceSummary>(
      `/api/v1/storage-maintenance/storages/${encodeURIComponent(storageId)}/stats`,
    );
    return result.data;
  }

  async startCleanup(storageId: string): Promise<StorageCleanupJob> {
    const result = await this.axios().post<StorageCleanupJob>(
      `/api/v1/storage-maintenance/storages/${encodeURIComponent(storageId)}/cleanup`,
    );
    return result.data;
  }

  async cancelCleanup(jobId: string): Promise<void> {
    await this.axios().post(
      `/api/v1/storage-maintenance/jobs/${encodeURIComponent(jobId)}/cancel`,
    );
  }
}

export function useStorageMaintenanceApi(): StorageMaintenanceApiClient {
  const axios = useAxios();
  return useMemo(() => new StorageMaintenanceApiClient(() => axios), [axios]);
}

export type { StorageMaintenanceApiClient };
