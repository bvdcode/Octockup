import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type { ScheduleItem, CreateScheduleRequest, BackupItem } from "../types/api";
import { BackupStatus } from "../types/api";

class SchedulesApiClient {
  private axiosFactory: () => AxiosInstance;
  constructor(axiosFactory: () => AxiosInstance) { this.axiosFactory = axiosFactory; }
  private axios(): AxiosInstance { return this.axiosFactory(); }

  async list(): Promise<ScheduleItem[]> {
    const { data } = await this.axios().get<Array<any>>("/api/v1/schedules");
    return data.map(x => {
      const backup: BackupItem = {
        id: x.backup.id,
        tag: x.backup.tag,
        sourceId: x.backup.sourceId,
        storageId: x.backup.storageId,
        ignoredPaths: x.backup.ignoredPaths ?? [],
        source: x.backup.source,
        storage: x.backup.storage,
      };
      return {
        id: x.id,
        backupId: x.backupId,
        startAt: x.startAt,
        interval: x.interval,
        status: x.status as BackupStatus,
        finishedAt: x.finishedAt ?? null,
        errorMessage: x.errorMessage ?? null,
        backup,
      };
    });
  }

  async create(request: CreateScheduleRequest): Promise<void> {
    await this.axios().post("/api/v1/schedules", request);
  }
}

export function useSchedulesApi(): SchedulesApiClient {
  const axios = useAxios();
  return useMemo(() => new SchedulesApiClient(() => axios), [axios]);
}

export type { SchedulesApiClient };
