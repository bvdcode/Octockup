import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type { BackupItem, CreateBackupRequest } from "../types/api";

class BackupsApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<BackupItem[]> {
    const { data } = await this.axios().get<Array<any>>("/api/v1/backups");
    return data.map(x => ({
      id: x.id,
      tag: x.tag,
      sourceId: x.sourceId,
      storageId: x.storageId,
      ignoredPaths: x.ignoredPaths ?? [],
      source: x.source,
      storage: x.storage,
    }));
  }

  async create(request: CreateBackupRequest): Promise<void> {
    await this.axios().post("/api/v1/backups", request);
  }

  async delete(backupId: string): Promise<void> {
    await this.axios().delete(`/api/v1/backups/${encodeURIComponent(backupId)}`);
  }
}

export function useBackupsApi(): BackupsApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupsApiClient(() => axios), [axios]);
}

export type { BackupsApiClient };
