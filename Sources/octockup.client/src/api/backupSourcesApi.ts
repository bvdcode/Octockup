import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import { useMemo } from "react";
import type { BackupSource } from "../types/api";

class BackupSourcesApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<BackupSource[]> {
    const { data } = await this.axios().get<BackupSource[]>("/api/v1/backups/sources");
    return data;
  }

  async listAvailable(): Promise<BackupSource[]> {
    const { data } = await this.axios().get<BackupSource[]>("/api/v1/backups/sources/available");
    return data;
  }

  async create(backupSourceId: string, tag: string, parameters: Record<string, any>): Promise<any> {
    const { data } = await this.axios().post<any>(`/api/v1/backups/sources/${encodeURIComponent(
      backupSourceId,
    )}/create`, { tag, parameters });
    return data;
  }

  async test(id: string, parameters: Record<string, any>): Promise<any> {
    const { data } = await this.axios().post<any>(`/api/v1/backups/sources/${encodeURIComponent(
      id,
    )}/test`, { parameters });
    return data;
  }

  async getDirectories(id: string, parameters: Record<string, any>): Promise<string[]> {
    const { data } = await this.axios().post<string[]>(`/api/v1/backups/sources/${encodeURIComponent(
      id,
    )}/directories`, { parameters });
    return data;
  }
}

export function useBackupSourcesApi(): BackupSourcesApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupSourcesApiClient(() => axios), [axios]);
}

export type { BackupSourcesApiClient };