import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type { SnapshotFileDto } from "../types/api";

export interface SnapshotDto {
  id: string;
  backupId: string;
  completedAt?: string | null;
  filesCount: number;
  totalSize: number;
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

  async getFiles(snapshotId: string): Promise<SnapshotFileDto[]> {
    const result = await this.axios().get<Array<SnapshotFileDto>>(
      `/api/v1/snapshots/${snapshotId}/files`,
    );
    return result.data;
  }
}

export function useSnapshotsApi(): SnapshotsApiClient {
  const axios = useAxios();
  return useMemo(() => new SnapshotsApiClient(() => axios), [axios]);
}

export type { SnapshotsApiClient };
