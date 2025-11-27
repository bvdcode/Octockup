import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type { BackupSummary } from "../types/api";

class BackupsApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<BackupSummary[]> {
    const { data } = await this.axios().get<BackupSummary[]>("/api/v1/backups");
    return data;
  }
}

export function useBackupsApi(): BackupsApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupsApiClient(() => axios), [axios]);
}

export type { BackupsApiClient };
