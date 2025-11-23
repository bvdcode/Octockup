import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import { useMemo } from "react";

export interface BackupSource {
  id: string;
  name: string;
  parameters: string[];
}

class BackupSourcesApiClient {
  constructor(private axiosFactory: () => AxiosInstance) {}

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<BackupSource[]> {
    const { data } = await this.axios().get<BackupSource[]>("/api/v1/backups/sources");
    return data;
  }
}

export function useBackupSourcesApi(): BackupSourcesApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupSourcesApiClient(() => axios), [axios]);
}

export type { BackupSourcesApiClient };