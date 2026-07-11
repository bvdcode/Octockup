import type { AxiosInstance } from "axios";
import type {
  StorageCleanupJob,
  StorageMaintenanceSummary,
} from "../types/api";

export class StorageMaintenanceApiClient {
  public constructor(private readonly axiosFactory: () => AxiosInstance) {}

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  public async list(): Promise<StorageMaintenanceSummary[]> {
    const result = await this.axios().get<StorageMaintenanceSummary[]>(
      "/api/v1/storage-maintenance",
    );
    return result.data;
  }

  public async listJobs(): Promise<StorageCleanupJob[]> {
    const result = await this.axios().get<StorageCleanupJob[]>(
      "/api/v1/storage-maintenance/jobs",
    );
    return result.data;
  }

  public async getStats(storageId: string): Promise<StorageMaintenanceSummary> {
    const result = await this.axios().get<StorageMaintenanceSummary>(
      `/api/v1/storage-maintenance/storages/${encodeURIComponent(storageId)}/stats`,
    );
    return result.data;
  }

  public async startCleanup(storageId: string): Promise<StorageCleanupJob> {
    const result = await this.axios().post<StorageCleanupJob>(
      `/api/v1/storage-maintenance/storages/${encodeURIComponent(storageId)}/cleanup`,
    );
    return result.data;
  }

  public async cancelCleanup(jobId: string): Promise<void> {
    await this.axios().post(
      `/api/v1/storage-maintenance/jobs/${encodeURIComponent(jobId)}/cancel`,
    );
  }
}
