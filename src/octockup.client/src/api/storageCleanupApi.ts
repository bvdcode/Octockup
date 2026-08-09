import type { AxiosInstance } from "axios";
import { useMemo } from "react";
import { useAxios } from "@bvdcode/react-kit";
import type {
  StorageCleanup,
  StorageCleanupRun,
} from "../types/storageCleanup";

export class StorageCleanupApiClient {
  constructor(private readonly axiosInstance: AxiosInstance) {}

  async list(): Promise<StorageCleanup[]> {
    const response = await this.axiosInstance.get<StorageCleanup[]>(
      "/api/v1/admin/storage-cleanups",
    );
    return response.data;
  }

  async start(moduleId: string): Promise<StorageCleanup> {
    const response = await this.axiosInstance.post<StorageCleanup>(
      `/api/v1/admin/storage-cleanups/${encodeURIComponent(moduleId)}/start`,
    );
    return response.data;
  }

  async listRuns(limit = 50): Promise<StorageCleanupRun[]> {
    const response = await this.axiosInstance.get<StorageCleanupRun[]>(
      "/api/v1/admin/storage-cleanups/runs",
      { params: { limit } },
    );
    return response.data;
  }
}

export function useStorageCleanupApi(): StorageCleanupApiClient {
  const axiosInstance = useAxios();
  return useMemo(
    () => new StorageCleanupApiClient(axiosInstance),
    [axiosInstance],
  );
}
