import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type {
  BackupDeletionResult,
  BackupItem,
  CreateBackupRequest,
  DownloadTicket,
} from "../types/api";

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

  async delete(backupId: string): Promise<BackupDeletionResult> {
    const result = await this.axios().delete<BackupDeletionResult>(
      `/api/v1/backups/${encodeURIComponent(backupId)}`,
    );
    return result.data;
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

  async importServerBackup(file: File): Promise<{ message: string }> {
    const formData = new FormData();
    formData.append("file", file);
    const result = await this.axios().post<{ message: string }>(
      "/api/v1/backups/server/import",
      formData,
    );
    return result.data;
  }

  async createServerBackupDownloadTicket(
    includeFiles: boolean,
  ): Promise<DownloadTicket> {
    const result = await this.axios().post<DownloadTicket>(
      "/api/v1/download-tickets/server-backup",
      undefined,
      { params: { includeFiles } },
    );
    return result.data;
  }
}

export function useBackupsApi(): BackupsApiClient {
  const axios = useAxios();
  return useMemo(() => new BackupsApiClient(() => axios), [axios]);
}

export type { BackupsApiClient };
