import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type {
  DownloadTicket,
  SnapshotArchiveJob,
  SnapshotDto,
  SnapshotFilePage,
  SnapshotDeletionResult,
} from "../types/api";

interface SnapshotFilePageRequest {
  pageSize: number;
  cursor?: string;
  search?: string;
}

class SnapshotsApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async listByBackup(backupId: string): Promise<SnapshotDto[]> {
    const result = await this.axios().get<Array<SnapshotDto>>(
      "/api/v1/snapshots",
      { params: { backupId } },
    );
    return result.data;
  }

  async getFiles(
    snapshotId: string,
    request: SnapshotFilePageRequest,
  ): Promise<SnapshotFilePage> {
    const search = request.search?.trim();
    const result = await this.axios().get<SnapshotFilePage>(
      `/api/v1/snapshots/${snapshotId}/files`,
      {
        params: {
          pageSize: request.pageSize,
          cursor: request.cursor,
          search: search || undefined,
        },
      },
    );
    return result.data;
  }

  async delete(snapshotId: string): Promise<SnapshotDeletionResult> {
    const result = await this.axios().delete<SnapshotDeletionResult>(
      `/api/v1/snapshots/${snapshotId}`,
    );
    return result.data;
  }

  async listArchiveJobs(backupId: string): Promise<SnapshotArchiveJob[]> {
    const result = await this.axios().get<SnapshotArchiveJob[]>(
      "/api/v1/snapshot-archive-jobs",
      { params: { backupId } },
    );
    return result.data;
  }

  async startArchiveJob(snapshotId: string): Promise<SnapshotArchiveJob> {
    const result = await this.axios().post<SnapshotArchiveJob>(
      `/api/v1/snapshots/${encodeURIComponent(snapshotId)}/archive-jobs`,
    );
    return result.data;
  }

  async cancelArchiveJob(jobId: string): Promise<void> {
    await this.axios().post(
      `/api/v1/snapshot-archive-jobs/${encodeURIComponent(jobId)}/cancel`,
    );
  }

  async createArchiveJobDownloadTicket(
    jobId: string,
  ): Promise<DownloadTicket> {
    const result = await this.axios().post<DownloadTicket>(
      `/api/v1/download-tickets/snapshot-archive-jobs/${encodeURIComponent(jobId)}`,
    );
    return result.data;
  }

  async createFileDownloadTicket(
    snapshotId: string,
    fileId: string,
  ): Promise<DownloadTicket> {
    const result = await this.axios().post<DownloadTicket>(
      `/api/v1/download-tickets/snapshots/${encodeURIComponent(snapshotId)}/files/${encodeURIComponent(fileId)}`,
    );
    return result.data;
  }
}

export function useSnapshotsApi(): SnapshotsApiClient {
  const axios = useAxios();
  return useMemo(() => new SnapshotsApiClient(() => axios), [axios]);
}

export type { SnapshotsApiClient };
