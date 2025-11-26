import {
  Box,
  Alert,
  Stack,
  Button,
  Snackbar,
  Typography,
  CircularProgress,
} from "@mui/material";
import type { ClipboardEvent } from "react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { TestActions } from "./wizard/TestActions";
import { ModuleHeader } from "./wizard/ModuleHeader";
import { useWizardForm } from "../hooks/useWizardForm";
import { useModuleTest } from "../hooks/useModuleTest";
import { ParametersForm } from "./wizard/ParametersForm";
import { DirectoryBrowser } from "./wizard/DirectoryBrowser";
import { ArrowBack, CheckCircle } from "@mui/icons-material";
import { useModuleMetadata } from "../hooks/useModuleMetadata";
import { useNavigate, useSearchParams } from "react-router-dom";
import type { BackupSource, BackupStorage } from "../types/api";
import { useDirectoryBrowser } from "../hooks/useDirectoryBrowser";

type ModuleType = "source" | "storage";

interface BackupModuleWizardProps {
  moduleType: ModuleType;
  apiClient: {
    listAvailable: () => Promise<(BackupSource | BackupStorage)[]>;
    create: (
      backupModuleId: string,
      tag: string,
      parameters: Record<string, string>,
    ) => Promise<void>;
    test: (id: string, parameters: Record<string, string>) => Promise<unknown>;
    getDirectories: (
      id: string,
      parameters: Record<string, string>,
    ) => Promise<string[]>;
  };
  backRoute: string;
}

export default function BackupModuleWizard({
  moduleType,
  apiClient,
  backRoute,
}: BackupModuleWizardProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const typeId = searchParams.get("type") || "";

  const { loading, error, moduleMeta } = useModuleMetadata(typeId, apiClient);
  const {
    params,
    tag,
    hasUnsavedChanges,
    initializeParams,
    updateParam,
    updateTag,
    resetUnsavedChanges,
    bulkUpdateParams,
  } = useWizardForm(moduleMeta);

  const browser = useDirectoryBrowser(moduleMeta, params, typeId, apiClient);
  const test = useModuleTest(moduleMeta, params, typeId, apiClient);

  const [creating, setCreating] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [showSuccessToast, setShowSuccessToast] = useState(false);

  // Initialize params when metadata loads
  useEffect(() => {
    initializeParams();
  }, [initializeParams]);

  // Reset test state when params change (except path)
  const handleParamChange = (name: string, value: string) => {
    updateParam(name, value);
    test.resetTest();
    if (name !== "path" && moduleMeta?.parameters.includes("path")) {
      browser.resetBrowser();
    }
  };

  // Handle multi-line paste
  const handleParamsPaste = (e: ClipboardEvent<HTMLInputElement>) => {
    if (!moduleMeta) return;
    const text = e.clipboardData?.getData("text") ?? "";
    if (!text) return;
    const rawLines = text.split(/\r?\n/);
    if (rawLines.length === 0) return;
    if (rawLines[rawLines.length - 1] === "") rawLines.pop();
    const lines = rawLines;
    const keys = moduleMeta.parameters || [];
    if (lines.length !== keys.length) return;

    e.preventDefault();
    const next: Record<string, string> = { ...params };
    keys.forEach((k, i) => {
      next[k] = lines[i];
    });
    bulkUpdateParams(next);
    test.resetTest();

    if (keys.includes("path")) {
      const pastedPath = next["path"] || "";
      browser.loadDirectories(pastedPath);
    }
  };

  // Handle directory navigation with path sync
  const handleNavigateToDir = (dir: string) => {
    browser.navigateToDir(dir);
    if (moduleMeta?.parameters.includes("path")) {
      const sep = moduleMeta.pathSeparator || "/";
      const newPath =
        browser.browserPath === sep
          ? `${browser.browserPath}${dir}`
          : `${browser.browserPath}${sep}${dir}`;
      updateParam("path", newPath);
    }
  };

  const handleNavigateUp = () => {
    if (!moduleMeta) return;
    const sep = moduleMeta.pathSeparator || "/";
    const lastSepIndex = browser.browserPath.lastIndexOf(sep);
    const newPath =
      lastSepIndex <= 0 ? sep : browser.browserPath.substring(0, lastSepIndex);
    browser.navigateUp();
    if (moduleMeta.parameters.includes("path")) {
      updateParam("path", newPath);
    }
  };

  const handleNavigateToRoot = () => {
    if (!moduleMeta) return;
    const sep = moduleMeta.pathSeparator || "/";
    browser.navigateToRoot();
    if (moduleMeta.parameters.includes("path")) {
      updateParam("path", sep);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!moduleMeta || !tag.trim()) {
      setSubmitError(t("wizard.fillParameters"));
      return;
    }
    const invalidHttp = test.validateHttpEndpoint();
    if (invalidHttp) {
      setSubmitError(invalidHttp);
      return;
    }
    try {
      setCreating(true);
      setSubmitError(null);
      await apiClient.create(typeId, tag.trim(), params);
      resetUnsavedChanges();
      setShowSuccessToast(true);
      setTimeout(() => {
        navigate(backRoute);
      }, 1000);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      setSubmitError(msg || t("wizard.createError"));
    } finally {
      setCreating(false);
    }
  };

  const handleBack = () => {
    if (hasUnsavedChanges) {
      const confirmed = window.confirm(t("wizard.unsavedChanges"));
      if (!confirmed) return;
    }
    navigate(backRoute);
  };

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Box p={2}>
        <Alert severity="error">{error}</Alert>
        <Button
          sx={{ mt: 2 }}
          variant="outlined"
          startIcon={<ArrowBack />}
          onClick={() => navigate(backRoute)}
        >
          {t("common.back")}
        </Button>
      </Box>
    );
  }

  return (
    <>
      <Stack spacing={3}>
        <Stack direction="row" spacing={2} alignItems="center">
          <Button
            variant="outlined"
            startIcon={<ArrowBack />}
            onClick={handleBack}
            disabled={creating}
          >
            {t("common.back")}
          </Button>
          <Typography variant="h5">
            {t(
              moduleType === "source"
                ? "sources.newSource"
                : "storages.newStorage",
            )}
          </Typography>
        </Stack>
        {moduleMeta && (
          <Box component="form" onSubmit={handleSubmit}>
            <Stack spacing={3}>
              <ModuleHeader moduleMeta={moduleMeta} />

              <ParametersForm
                moduleMeta={moduleMeta}
                params={params}
                tag={tag}
                onParamChange={handleParamChange}
                onTagChange={updateTag}
                onParamsPaste={handleParamsPaste}
                disabled={creating}
              />

              {moduleMeta.parameters.includes("path") && (
                <DirectoryBrowser
                  browserPath={browser.browserPath}
                  browserDirs={browser.browserDirs}
                  browserLoading={browser.browserLoading}
                  onNavigateToRoot={handleNavigateToRoot}
                  onNavigateUp={handleNavigateUp}
                  onNavigateToDir={handleNavigateToDir}
                  disabled={creating}
                />
              )}

              {submitError && <Alert severity="error">{submitError}</Alert>}

              <TestActions
                testLoading={test.testLoading}
                testMessage={test.testMessage}
                testError={test.testError}
                creating={creating}
                moduleType={moduleType}
                onTest={test.runTest}
              />
            </Stack>
          </Box>
        )}
      </Stack>

      <Snackbar
        open={showSuccessToast}
        autoHideDuration={3000}
        onClose={() => setShowSuccessToast(false)}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      >
        <Alert severity="success" icon={<CheckCircle />} sx={{ width: "100%" }}>
          {t(
            moduleType === "source"
              ? "wizard.sourceCreatedSuccess"
              : "wizard.storageCreatedSuccess",
          )}
        </Alert>
      </Snackbar>
    </>
  );
}
