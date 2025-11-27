import { useMemo } from "react";
import type { AxiosInstance } from "axios";
import { useAxios } from "@bvdcode/react-kit";
import { ModuleDestination } from "../types/api";
import type { Module, ModuleProviderInfo, CreateModuleRequest } from "../types/api";

class ModulesApiClient {
  private axiosFactory: () => AxiosInstance;

  constructor(axiosFactory: () => AxiosInstance) {
    this.axiosFactory = axiosFactory;
  }

  private axios(): AxiosInstance {
    return this.axiosFactory();
  }

  async list(): Promise<Module[]> {
    const { data } = await this.axios().get<Module[]>("/api/v1/modules");
    return data;
  }

  async listProviders(): Promise<ModuleProviderInfo[]> {
    const { data } = await this.axios().get<ModuleProviderInfo[]>("/api/v1/modules/providers");
    return data;
  }

  async listProvidersByType(type: 'source' | 'target'): Promise<ModuleProviderInfo[]> {
    const { data } = await this.axios().get<ModuleProviderInfo[]>(`/api/v1/modules/providers/${encodeURIComponent(type)}`);
    return data;
  }

  async create(providerId: string, destination: ModuleDestination, tag: string, backupModuleId: string, parameters: Record<string, string>): Promise<void> {
    const body: CreateModuleRequest = { destination, tag, backupModuleId, parameters };
    await this.axios().post(`/api/v1/modules/providers/${encodeURIComponent(providerId)}`, body);
  }

  async test(providerId: string, parameters: Record<string, string>): Promise<void> {
    await this.axios().post(`/api/v1/modules/providers/${encodeURIComponent(providerId)}/test`, { parameters });
  }

  async getDirectories(providerId: string, parameters: Record<string, string>): Promise<string[]> {
    const { data } = await this.axios().post<string[]>(`/api/v1/modules/providers/${encodeURIComponent(providerId)}/directories`, { parameters });
    return data;
  }

  async delete(moduleId: string): Promise<void> {
    await this.axios().delete(`/api/v1/modules/${encodeURIComponent(moduleId)}`);
  }
}

export function useModulesApi(): ModulesApiClient {
  const axios = useAxios();
  return useMemo(() => new ModulesApiClient(() => axios), [axios]);
}

export type { ModulesApiClient };