import type { AxiosInstance } from "axios";
import type {
  DownloadTicket,
  SnapshotArchiveJob,
  SnapshotDeletionResult,
  SnapshotFilePage,
  SnapshotPage,
} from "../types/api";

export interface SnapshotPageRequest {
  pageSize: number;
  cursor?: string;
}

export interface SnapshotFilePageRequest {
  pageSize: number;
  cursor?: string;
  search?: string;
}

export class SnapshotsApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async listByBackup(
    backupId: string,
    request: SnapshotPageRequest,
  ): Promise<SnapshotPage> {
    const result = await this.axios().get<SnapshotPage>(
      "/api/v1/snapshots",
      {
        params: {
          backupId,
          pageSize: request.pageSize,
          cursor: request.cursor,
        },
      },
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

  async listArchiveJobs(
    snapshotIds: string[],
  ): Promise<SnapshotArchiveJob[]> {
    const result = await this.axios().post<SnapshotArchiveJob[]>(
      "/api/v1/snapshot-archive-jobs/query",
      { snapshotIds },
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
