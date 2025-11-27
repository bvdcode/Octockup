import { useState, useCallback } from "react";
import type { ModuleProviderInfo } from "../types/api";

interface ParamState {
  [key: string]: string;
}

export function useWizardForm(moduleMeta: ModuleProviderInfo | null) {
  const [params, setParams] = useState<ParamState>({});
  const [tag, setTag] = useState<string>("");
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

  const initializeParams = useCallback(() => {
    if (!moduleMeta) return;
    const initial: ParamState = {};
    moduleMeta.requiredParameters.forEach((p) => (initial[p] = ""));
    setParams(initial);
  }, [moduleMeta]);

  const updateParam = useCallback((name: string, value: string) => {
    setParams((prev) => ({ ...prev, [name]: value }));
    setHasUnsavedChanges(true);
  }, []);

  const updateTag = useCallback((value: string) => {
    setTag(value);
    setHasUnsavedChanges(true);
  }, []);

  const resetUnsavedChanges = useCallback(() => {
    setHasUnsavedChanges(false);
  }, []);

  const bulkUpdateParams = useCallback((newParams: ParamState) => {
    setParams(newParams);
    setHasUnsavedChanges(true);
  }, []);

  return {
    params,
    tag,
    hasUnsavedChanges,
    initializeParams,
    updateParam,
    updateTag,
    resetUnsavedChanges,
    bulkUpdateParams,
  };
}
