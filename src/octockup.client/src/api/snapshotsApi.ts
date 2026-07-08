import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type {
  SnapshotDto,
  SnapshotFileDto,
  SnapshotDeletionResult,
} from "../types/api";

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

  async getFiles(snapshotId: string): Promise<SnapshotFileDto[]> {
    const result = await this.axios().get<Array<SnapshotFileDto>>(
      `/api/v1/snapshots/${snapshotId}/files`,
    );
    return result.data;
  }

  async delete(snapshotId: string): Promise<SnapshotDeletionResult> {
    const result = await this.axios().delete<SnapshotDeletionResult>(
      `/api/v1/snapshots/${snapshotId}`,
    );
    return result.data;
  }
}

export function useSnapshotsApi(): SnapshotsApiClient {
  const axios = useAxios();
  return useMemo(() => new SnapshotsApiClient(() => axios), [axios]);
}

export type { SnapshotsApiClient };
