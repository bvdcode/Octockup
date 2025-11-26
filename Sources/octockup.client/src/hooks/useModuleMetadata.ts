import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { BackupSource, BackupStorage } from "../types/api";

export function useModuleMetadata(
  typeId: string,
  apiClient: {
    listAvailable: () => Promise<(BackupSource | BackupStorage)[]>;
  },
) {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [moduleMeta, setModuleMeta] = useState<
    BackupSource | BackupStorage | null
  >(null);

  useEffect(() => {
    let active = true;

    const load = async () => {
      if (!typeId) {
        setError(t("wizard.typeNotSpecified"));
        setLoading(false);
        return;
      }

      try {
        const all = await apiClient.listAvailable();
        if (!active) return;

        const meta = all.find((x) => x.id === typeId);
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
  }, [apiClient, typeId, t]);

  return { loading, error, moduleMeta };
}
