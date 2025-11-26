import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import { useMemo } from "react";
import type { BackupSource, SavedBackupModule } from "../types/api";

class BackupSourcesApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<SavedBackupModule[]> {
    const { data } = await this.axios().get<SavedBackupModule[]>("/api/v1/backups/sources");
    return data;
  }

  async listAvailable(): Promise<BackupSource[]> {
    const { data } = await this.axios().get<BackupSource[]>("/api/v1/backups/sources/available");
    return data;
  }

  async create(backupModuleId: string, tag: string, parameters: Record<string, any>): Promise<any> {
    try {
      const { data } = await this.axios().post<any>(`/api/v1/backups/sources/${encodeURIComponent(
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
      const { data } = await this.axios().post<any>(`/api/v1/backups/sources/${encodeURIComponent(
        id,
      )}/test`, { parameters });
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
      const { data } = await this.axios().post<string[]>(`/api/v1/backups/sources/${encodeURIComponent(
        id,
      )}/directories`, { parameters });
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

export function useBackupSourcesApi(): BackupSourcesApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupSourcesApiClient(() => axios), [axios]);
}

export type { BackupSourcesApiClient };