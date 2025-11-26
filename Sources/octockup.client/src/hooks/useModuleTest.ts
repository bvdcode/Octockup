import { useState, useCallback } from "react";
import { useTranslation } from "react-i18next";
import type { BackupSource, BackupStorage } from "../types/api";

interface ParamState {
  [key: string]: string;
}

export function useModuleTest(
  moduleMeta: BackupSource | BackupStorage | null,
  params: ParamState,
  typeId: string,
  apiClient: {
    test: (id: string, parameters: Record<string, string>) => Promise<unknown>;
  },
) {
  const { t } = useTranslation();
  const [testLoading, setTestLoading] = useState(false);
  const [testMessage, setTestMessage] = useState<string | null>(null);
  const [testError, setTestError] = useState<string | null>(null);

  const validateHttpEndpoint = useCallback((): string | null => {
    if (!moduleMeta) return null;
    if (!moduleMeta.parameters.includes("httpEndpoint")) return null;
    const ep = (params["httpEndpoint"] || "").trim();
    if (!ep) return null;
    if (!/^https?:\/\//i.test(ep)) {
      return t("wizard.invalidHttpEndpoint");
    }
    return null;
  }, [moduleMeta, params, t]);

  const runTest = useCallback(async () => {
    setTestError(null);
    setTestMessage(null);
    if (!moduleMeta) return;

    const invalidHttp = validateHttpEndpoint();
    if (invalidHttp) {
      setTestError(invalidHttp);
      return;
    }

    const required = moduleMeta.parameters || [];
    const missing = required.filter(
      (p) => !(params[p] && String(params[p]).length > 0),
    );
    if (missing.length > 0) {
      setTestError(t("wizard.fillParameters"));
      return;
    }

    try {
      setTestLoading(true);
      await apiClient.test(typeId, params);
      setTestMessage(t("wizard.testSuccess"));
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      setTestError(msg || t("wizard.testFailed"));
    } finally {
      setTestLoading(false);
    }
  }, [moduleMeta, params, typeId, apiClient, t, validateHttpEndpoint]);

  const resetTest = useCallback(() => {
    setTestMessage(null);
    setTestError(null);
  }, []);

  return {
    testLoading,
    testMessage,
    testError,
    runTest,
    resetTest,
    validateHttpEndpoint,
  };
}
