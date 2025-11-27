import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type { CreateTaskRequest, TaskItem } from "../types/api";
import { TaskStatus } from "../types/api";

class TasksApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<TaskItem[]> {
    const { data } = await this.axios().get<Array<any>>("/api/v1/tasks");
    return data.map((x) => ({
      id: x.id,
      backupId: x.backupId,
      startAt: x.startAt,
      interval: x.interval,
      status: x.status as TaskStatus,
      finishedAt: x.finishedAt ?? null,
      errorMessage: x.errorMessage ?? null,
      backupTag: x.backup_Tag,
      sourceTag: x.backup_SourceTag,
      storageTag: x.backup_StorageTag,
      sourceProviderId: x.backup_SourceProviderId,
      storageProviderId: x.backup_StorageProviderId,
    }));
  }

  async create(request: CreateTaskRequest): Promise<void> {
    await this.axios().post("/api/v1/tasks", request);
  }
}

export function useTasksApi(): TasksApiClient {
  const axios = useAxios();
  return useMemo(() => new TasksApiClient(() => axios), [axios]);
}

export type { TasksApiClient };
