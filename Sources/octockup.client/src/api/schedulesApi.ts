import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type { ScheduleItem, CreateScheduleRequest } from "../types/api";
import { BackupStatus } from "../types/api";

class SchedulesApiClient {
  private axiosFactory: () => AxiosInstance;
  constructor(axiosFactory: () => AxiosInstance) { this.axiosFactory = axiosFactory; }
  private axios(): AxiosInstance { return this.axiosFactory(); }

  async list(): Promise<ScheduleItem[]> {
    const { data } = await this.axios().get<Array<any>>("/api/v1/schedules");
    return data.map(x => ({
      id: x.id,
      backupId: x.backupId,
      startAt: x.startAt,
      interval: x.interval,
      status: x.status as BackupStatus,
      finishedAt: x.finishedAt ?? null,
      errorMessage: x.errorMessage ?? null,
      backupTag: x.backup_Tag,
      sourceTag: x.backup_SourceTag,
      storageTag: x.backup_StorageTag,
      sourceProviderId: x.backup_SourceProviderId,
      storageProviderId: x.backup_StorageProviderId,
    }));
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
