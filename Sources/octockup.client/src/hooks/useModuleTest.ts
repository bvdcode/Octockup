import { useState, useCallback } from "react";
import { useTranslation } from "react-i18next";
import type { ModuleProviderInfo, ModuleDestination } from "../types/api";

interface ParamState {
  [key: string]: string;
}

export function useModuleTest(
  moduleMeta: ModuleProviderInfo | null,
  params: ParamState,
  providerId: string,
  destination: ModuleDestination,
  apiClient: {
    test: (
      id: string,
      parameters: Record<string, string>,
      destination: ModuleDestination,
    ) => Promise<void>;
  },
) {
  const { t } = useTranslation();
  const [testLoading, setTestLoading] = useState(false);
  const [testMessage, setTestMessage] = useState<string | null>(null);
  const [testError, setTestError] = useState<string | null>(null);

  const validateHttpEndpoint = useCallback((): string | null => {
    if (!moduleMeta) return null;
    if (!moduleMeta.requiredParameters.includes("httpEndpoint")) return null;
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

    const CHECKBOX_PARAMS = ["skipPermissionDenied", "validateChecksums"];
    const required = moduleMeta.requiredParameters || [];
    // Don't require checkbox parameters to be filled for testing
    const missing = required.filter(
      (p) => !CHECKBOX_PARAMS.includes(p) && !(params[p] && String(params[p]).length > 0),
    );
    if (missing.length > 0) {
      setTestError(t("wizard.fillParameters"));
      return;
    }

    try {
      setTestLoading(true);
      await apiClient.test(providerId, params, destination);
      setTestMessage(t("wizard.testSuccess"));
    } catch (err: unknown) {
      let msg = t("wizard.testFailed");
      if (err && typeof err === "object") {
        const response = (err as { response?: { data?: { detail?: string } } })
          .response;
        if (response?.data?.detail) {
          msg = response.data.detail;
        } else if ("message" in err && typeof err.message === "string") {
          msg = err.message;
        }
      } else if (err instanceof Error) {
        msg = err.message;
      }
      setTestError(msg);
    } finally {
      setTestLoading(false);
    }
  }, [
    moduleMeta,
    params,
    providerId,
    destination,
    apiClient,
    t,
    validateHttpEndpoint,
  ]);

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
