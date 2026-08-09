import { useMemo } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useModulesApi } from "../api/modulesApi";
import { queryKeys } from "../query/queryKeys";
import { ModuleDestination, type Module } from "../types/api";

export function useModuleCatalog(
  destination: ModuleDestination,
  providerType: "source" | "storage",
) {
  const api = useModulesApi();
  const queryClient = useQueryClient();
  const modulesQuery = useQuery({
    queryKey: queryKeys.modules,
    queryFn: () => api.list(),
  });
  const providersQuery = useQuery({
    queryKey: queryKeys.moduleProviders(providerType),
    queryFn: () => api.listProvidersByType(providerType),
  });

  const modules = useMemo(
    () =>
      (modulesQuery.data ?? []).filter(
        (module) => module.destination === destination,
      ),
    [destination, modulesQuery.data],
  );

  const updateModules = (updater: (current: Module[]) => Module[]) => {
    queryClient.setQueryData<Module[]>(queryKeys.modules, (current) =>
      updater(current ?? []),
    );
  };

  const renameModule = async (moduleId: string, newTag: string) => {
    const trimmedTag = newTag.trim();
    await api.rename(moduleId, trimmedTag);
    updateModules((current) =>
      current.map((module) =>
        module.id === moduleId ? { ...module, tag: trimmedTag } : module,
      ),
    );
    await queryClient.invalidateQueries({ queryKey: queryKeys.backups });
  };

  const deleteModule = async (moduleId: string) => {
    await api.delete(moduleId);
    updateModules((current) =>
      current.filter((module) => module.id !== moduleId),
    );
    await queryClient.invalidateQueries({ queryKey: queryKeys.backups });
  };

  return {
    modules,
    providers: providersQuery.data ?? [],
    isPending: modulesQuery.isPending || providersQuery.isPending,
    hasData:
      modulesQuery.data !== undefined && providersQuery.data !== undefined,
    error: modulesQuery.error ?? providersQuery.error,
    renameModule,
    deleteModule,
  };
}
