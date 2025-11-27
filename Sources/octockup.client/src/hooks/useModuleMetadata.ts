import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ModuleProviderInfo } from "../types/api";

export function useModuleMetadata(
  providerId: string,
  apiClient: { listProviders: () => Promise<ModuleProviderInfo[]> },
) {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [moduleMeta, setModuleMeta] = useState<ModuleProviderInfo | null>(null);

  useEffect(() => {
    let active = true;

    const load = async () => {
      if (!providerId) {
        setError(t("wizard.typeNotSpecified"));
        setLoading(false);
        return;
      }

      try {
        const all = await apiClient.listProviders();
        if (!active) return;

        const meta = all.find((x) => x.id === providerId);
        if (!meta) {
          setError(t("wizard.typeNotFound"));
        } else {
          setModuleMeta(meta);
        }
        setLoading(false);
      } catch (e: unknown) {
        if (!active) return;
        setError(e instanceof Error ? e.message : t("wizard.loadError"));
        setLoading(false);
      }
    };

    load();

    return () => {
      active = false;
    };
  }, [apiClient, providerId, t]);

  return { loading, error, moduleMeta };
}
