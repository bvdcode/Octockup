import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import type { ScheduleItem, CreateScheduleRequest } from "../types/api";

class SchedulesApiClient {
  private axiosFactory: () => AxiosInstance;
  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }
  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<ScheduleItem[]> {
    const result = await this.axios().get<Array<ScheduleItem>>(
      "/api/v1/schedules",
    );
    return result.data;
  }

  async create(request: CreateScheduleRequest): Promise<void> {
    await this.axios().post("/api/v1/schedules", request);
  }

  async delete(scheduleId: string): Promise<void> {
    await this.axios().delete(`/api/v1/schedules/${scheduleId}`);
  }

  async cancel(scheduleId: string): Promise<void> {
    await this.axios().post(`/api/v1/schedules/${scheduleId}/cancel`);
  }
}

export function useSchedulesApi(): SchedulesApiClient {
  const axios = useAxios();
  return useMemo(() => new SchedulesApiClient(() => axios), [axios]);
}

export type { SchedulesApiClient };
