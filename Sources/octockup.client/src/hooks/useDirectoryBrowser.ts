import { useState, useCallback } from "react";
import type { ModuleProviderInfo } from "../types/api";

interface ParamState {
  [key: string]: string;
}

export function useDirectoryBrowser(
  moduleMeta: ModuleProviderInfo | null,
  params: ParamState,
  providerId: string,
  apiClient: {
    getDirectories: (id: string, parameters: Record<string, string>) => Promise<string[]>;
  },
) {
  const [browserPath, setBrowserPath] = useState<string>("");
  const [browserDirs, setBrowserDirs] = useState<string[]>([]);
  const [browserLoading, setBrowserLoading] = useState(false);

  const loadDirectories = useCallback(
    async (targetPath: string) => {
      if (!moduleMeta || browserLoading) return;
      const required = moduleMeta.requiredParameters.filter((p) => p !== "path");
      const missing = required.filter(
        (p) => !(params[p] && String(params[p]).length > 0),
      );
      if (missing.length > 0) return;

      try {
        setBrowserLoading(true);
        const paramsWithPath = { ...params, path: targetPath };
        const dirs = await apiClient.getDirectories(providerId, paramsWithPath);
        setBrowserDirs(dirs || []);
        setBrowserPath(targetPath);
      } catch {
        setBrowserDirs([]);
      } finally {
        setBrowserLoading(false);
      }
    },
    [moduleMeta, browserLoading, params, apiClient, providerId],
  );

  const navigateToDir = useCallback(
    (dir: string) => {
      if (!moduleMeta) return;
      const sep = moduleMeta.pathSeparator || "/";
      const newPath =
        browserPath === sep
          ? `${browserPath}${dir}`
          : `${browserPath}${sep}${dir}`;
      loadDirectories(newPath);
    },
    [moduleMeta, browserPath, loadDirectories],
  );

  const navigateUp = useCallback(() => {
    if (!moduleMeta) return;
    const sep = moduleMeta.pathSeparator || "/";
    const lastSepIndex = browserPath.lastIndexOf(sep);
    const newPath = lastSepIndex <= 0 ? sep : browserPath.substring(0, lastSepIndex);
    loadDirectories(newPath);
  }, [moduleMeta, browserPath, loadDirectories]);

  const navigateToRoot = useCallback(() => {
    if (!moduleMeta) return;
    const sep = moduleMeta.pathSeparator || "/";
    loadDirectories(sep);
  }, [moduleMeta, loadDirectories]);

  const resetBrowser = useCallback(() => {
    setBrowserPath("");
    setBrowserDirs([]);
  }, []);

  return {
    browserPath,
    browserDirs,
    browserLoading,
    loadDirectories,
    navigateToDir,
    navigateUp,
    navigateToRoot,
    resetBrowser,
  };
}
