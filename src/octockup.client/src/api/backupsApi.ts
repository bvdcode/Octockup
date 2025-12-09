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
    const result = await this.axios().get<Array<BackupItem>>("/api/v1/backups");
    return result.data;
  }

  async create(request: CreateBackupRequest): Promise<void> {
    await this.axios().post("/api/v1/backups", request);
  }

  async delete(backupId: string): Promise<void> {
    await this.axios().delete(
      `/api/v1/backups/${encodeURIComponent(backupId)}`,
    );
  }

  async rename(backupId: string, newTag: string): Promise<void> {
    await this.axios().patch(
      `/api/v1/backups/${encodeURIComponent(backupId)}/rename`,
      { newTag },
    );
  }

  async updateIgnoredPaths(
    backupId: string,
    ignoredPaths: string[],
  ): Promise<void> {
    await this.axios().patch(
      `/api/v1/backups/${encodeURIComponent(backupId)}/ignored-paths`,
      ignoredPaths,
    );
  }
}

export function useBackupsApi(): BackupsApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupsApiClient(() => axios), [axios]);
}

export type { BackupsApiClient };
