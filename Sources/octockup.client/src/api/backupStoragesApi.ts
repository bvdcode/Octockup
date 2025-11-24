import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type { BackupStorage } from "../types/api";

class BackupStoragesApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<BackupStorage[]> {
    const { data } = await this.axios().get<BackupStorage[]>(
      "/api/v1/backups/storages",
    );
    return data;
  }

  async test(id: string, parameters: Record<string, any>): Promise<any> {
    const { data } = await this.axios().post<any>(
      `/api/v1/backups/storages/${encodeURIComponent(id)}/test`,
      { parameters },
    );
    return data;
  }

  async getDirectories(id: string, parameters: Record<string, any>): Promise<string[]> {
    const { data } = await this.axios().post<string[]>(
      `/api/v1/backups/storages/${encodeURIComponent(id)}/directories`,
      { parameters },
    );
    return data;
  }
}

export function useBackupStoragesApi(): BackupStoragesApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupStoragesApiClient(() => axios), [axios]);
}

export type { BackupStoragesApiClient };
