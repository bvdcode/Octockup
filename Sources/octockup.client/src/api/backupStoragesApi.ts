import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type { BackupStorage, SavedBackupModule } from "../types/api";

class BackupStoragesApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<SavedBackupModule[]> {
    const { data } = await this.axios().get<SavedBackupModule[]>(
      "/api/v1/backups/storages",
    );
    return data;
  }

  async listAvailable(): Promise<BackupStorage[]> {
    const { data } = await this.axios().get<BackupStorage[]>(
      "/api/v1/backups/storages/available",
    );
    return data;
  }

  async create(backupModuleId: string, tag: string, parameters: Record<string, any>): Promise<any> {
    try {
      const { data } = await this.axios().post<any>(`/api/v1/backups/storages/${encodeURIComponent(
        backupModuleId,
      )}/create`, { tag, parameters });
      return data;
    } catch (error: any) {
      const detail = error?.response?.data?.detail;
      if (detail) {
        throw new Error(detail);
      }
      throw error;
    }
  }

  async test(id: string, parameters: Record<string, any>): Promise<any> {
    try {
      const { data } = await this.axios().post<any>(
        `/api/v1/backups/storages/${encodeURIComponent(id)}/test`,
        { parameters },
      );
      return data;
    } catch (error: any) {
      const detail = error?.response?.data?.detail;
      if (detail) {
        throw new Error(detail);
      }
      throw error;
    }
  }

  async getDirectories(id: string, parameters: Record<string, any>): Promise<string[]> {
    try {
      const { data } = await this.axios().post<string[]>(
        `/api/v1/backups/storages/${encodeURIComponent(id)}/directories`,
        { parameters },
      );
      return data;
    } catch (error: any) {
      const detail = error?.response?.data?.detail;
      if (detail) {
        throw new Error(detail);
      }
      throw error;
    }
  }
}

export function useBackupStoragesApi(): BackupStoragesApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupStoragesApiClient(() => axios), [axios]);
}

export type { BackupStoragesApiClient };
