import {
  Box,
  Card,
  Alert,
  Stack,
  Paper,
  Button,
  TextField,
  Typography,
  CardContent,
  CircularProgress,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  IconButton,
  Snackbar,
} from "@mui/material";
import { useEffect, useState, useCallback } from "react";
import type { ClipboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router-dom";
import {
  ArrowBack,
  Folder,
  ArrowUpward,
  Home,
  ArrowRight,
  CheckCircle,
} from "@mui/icons-material";
import type { BackupSource, BackupStorage } from "../types/api";
import { getSourceIcon } from "../constants/sourceIcons";

interface ParamState {
  [key: string]: string;
}

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
  translations: {
    title: string;
    tag: string;
    enterTag: string;
    testConnection: string;
    testing: string;
    testSuccess: string;
    testFailed: string;
    fillParameters: string;
    testResult: string;
    fileName: string;
    filePath: string;
    fileSize: string;
    fileModified: string;
    directoryBrowser: string;
    up: string;
    noSubdirectories: string;
    clickToLoad: string;
    loadRootDirectories: string;
    create: string;
    back: string;
    creating: string;
    unsavedChanges: string;
    createSuccess: string;
    createError: string;
  };
}

export default function BackupModuleWizard({
  apiClient,
  backRoute,
  translations,
}: BackupModuleWizardProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const typeId = searchParams.get("type") || "";
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [moduleMeta, setModuleMeta] = useState<
    BackupSource | BackupStorage | null
  >(null);
  const [params, setParams] = useState<ParamState>({});
  const [tag, setTag] = useState<string>("");
  const [testLoading, setTestLoading] = useState(false);
  const [testMessage, setTestMessage] = useState<string | null>(null);
  const [testError, setTestError] = useState<string | null>(null);
  const [browserPath, setBrowserPath] = useState<string>("");
  const [browserDirs, setBrowserDirs] = useState<string[]>([]);
  const [browserLoading, setBrowserLoading] = useState(false);
  const [showSuccessToast, setShowSuccessToast] = useState(false);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

  useEffect(() => {
    let active = true;

    const load = async () => {
      if (!typeId) {
        setError("Type not specified");
        setLoading(false);
        return;
      }

      try {
        const all = await apiClient.listAvailable();
        if (!active) return;

        const meta = all.find((x) => x.id === typeId);
        if (!meta) {
          setError("Type not found");
        } else {
          setModuleMeta(meta);
          const initial: ParamState = {};
          meta.parameters.forEach((p) => (initial[p] = ""));
          setParams(initial);
        }
        setLoading(false);
      } catch (e: unknown) {
        if (!active) return;
        setError(e instanceof Error ? e.message : "Failed to load metadata");
        setLoading(false);
      }
    };

    load();

    return () => {
      active = false;
    };
  }, [apiClient, typeId, translations]);

  const loadBrowserDirectories = useCallback(
    async (targetPath: string) => {
      if (!moduleMeta || browserLoading) return;
      const required = moduleMeta.parameters.filter((p) => p !== "path");
      const missing = required.filter(
        (p) => !(params[p] && String(params[p]).length > 0),
      );
      if (missing.length > 0) {
        return;
      }
      try {
        setBrowserLoading(true);
        const paramsWithPath = { ...params, path: targetPath };
        const dirs = await apiClient.getDirectories(typeId, paramsWithPath);
        setBrowserDirs(dirs || []);
        setBrowserPath(targetPath);
        setParams((prev) => ({ ...prev, path: targetPath }));
        setTestMessage(null);
        setTestError(null);
      } catch {
        setBrowserDirs([]);
      } finally {
        setBrowserLoading(false);
      }
    },
    [moduleMeta, browserLoading, params, apiClient, typeId],
  );

  useEffect(() => {
    if (!moduleMeta || !moduleMeta.parameters.includes("path")) return;
    const required = moduleMeta.parameters.filter((p) => p !== "path");
    const allFilled = required.every(
      (p) => params[p] && String(params[p]).length > 0,
    );
    const sep = moduleMeta.pathSeparator || "/";
    if (
      allFilled &&
      browserPath === "" &&
      browserDirs.length === 0 &&
      !browserLoading
    ) {
      loadBrowserDirectories(sep);
    }
  }, [
    params,
    moduleMeta,
    browserPath,
    browserDirs.length,
    browserLoading,
    loadBrowserDirectories,
  ]);

  function updateParam(name: string, value: string) {
    setParams((prev) => ({ ...prev, [name]: value }));
    setTestMessage(null);
    setTestError(null);
    setHasUnsavedChanges(true);
    if (name !== "path" && moduleMeta?.parameters.includes("path")) {
      setBrowserPath("");
      setBrowserDirs([]);
    }
  }

  function handleBrowserNavigate(dir: string) {
    const sep = moduleMeta?.pathSeparator || "/";
    const newPath =
      browserPath === sep ? `${browserPath}${dir}` : `${browserPath}${sep}${dir}`;
    loadBrowserDirectories(newPath);
  }

  function handleBrowserUp() {
    const sep = moduleMeta?.pathSeparator || "/";
    const lastSepIndex = browserPath.lastIndexOf(sep);
    const newPath = lastSepIndex <= 0 ? sep : browserPath.substring(0, lastSepIndex);
    loadBrowserDirectories(newPath);
  }

  function handleParamsPaste(e: ClipboardEvent<HTMLInputElement>) {
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
    const next: ParamState = { ...params };
    keys.forEach((k, i) => {
      next[k] = lines[i];
    });
    setParams(next);
    setHasUnsavedChanges(true);
    setTestMessage(null);
    setTestError(null);

    if (keys.includes("path")) {
      const pastedPath = next["path"] || "";
      setBrowserPath(pastedPath);
      setBrowserDirs([]);
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!moduleMeta || !tag.trim()) {
      setError(t("wizard.fillParameters"));
      return;
    }
    try {
      setCreating(true);
      setError(null);
      await apiClient.create(typeId, tag.trim(), params);
      setHasUnsavedChanges(false);
      setShowSuccessToast(true);
      setTimeout(() => {
        navigate(backRoute);
      }, 1000);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      setError(msg || t("wizard.createError"));
    } finally {
      setCreating(false);
    }
  }

  async function handleTest() {
    setTestError(null);
    setTestMessage(null);
    if (!moduleMeta) return;
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
  }

  function handleBack() {
    if (hasUnsavedChanges) {
      const confirmed = window.confirm(t("wizard.unsavedChanges"));
      if (!confirmed) return;
    }
    navigate(backRoute);
  }

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
          {translations.back}
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
            {translations.back}
          </Button>
          <Typography variant="h5">{translations.title}</Typography>
        </Stack>
        {moduleMeta && (
          <Box component="form" onSubmit={handleSubmit}>
            <Stack spacing={3}>
              <Card variant="outlined">
                <CardContent>
                  <Stack direction="row" spacing={2} alignItems="center">
                    <Box sx={{ fontSize: 42 }}>
                      {getSourceIcon(moduleMeta.id)}
                    </Box>
                    <Box>
                      <Typography variant="h6">{moduleMeta.name}</Typography>
                      <Typography variant="caption" color="text.secondary">
                        {moduleMeta.id}
                      </Typography>
                    </Box>
                  </Stack>
                </CardContent>
              </Card>

              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Parameters
                  </Typography>
                  <Stack spacing={2}>
                    <TextField
                      required
                      fullWidth
                      label={t("wizard.tag")}
                      value={tag}
                      onChange={(e) => {
                        setTag(e.target.value);
                        setHasUnsavedChanges(true);
                      }}
                      placeholder={t("wizard.enterTag")}
                      disabled={creating}
                    />
                    {moduleMeta.parameters.length === 0 ? (
                      <Typography
                        variant="body2"
                        color="text.secondary"
                        fontStyle="italic"
                      >
                        No parameters required.
                      </Typography>
                    ) : (
                      <>
                        {moduleMeta.parameters.map((p) => (
                          <TextField
                            key={p}
                            required={p !== "path"}
                            fullWidth
                            label={p}
                            value={params[p] ?? ""}
                            onChange={(e) => updateParam(p, e.target.value)}
                            onPaste={handleParamsPaste}
                            placeholder={t("wizard.enterValue", { param: p })}
                            disabled={creating}
                          />
                        ))}
                        {moduleMeta.parameters.includes("path") && (
                          <Paper
                            variant="outlined"
                            sx={{ p: 2, maxHeight: 300, overflow: "auto" }}
                          >
                            <Stack spacing={1}>
                              <Stack
                                direction="row"
                                alignItems="center"
                                justifyContent="space-between"
                              >
                                <Box
                                  sx={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 0.5,
                                  }}
                                >
                                  <IconButton
                                    size="small"
                                    onClick={() => {
                                      const sep =
                                        moduleMeta?.pathSeparator || "/";
                                      loadBrowserDirectories(sep);
                                    }}
                                    disabled={creating}
                                  >
                                    <Home fontSize="small" />
                                  </IconButton>
                                  <ArrowRight fontSize="small" />
                                  <Typography
                                    variant="caption"
                                    color="text.secondary"
                                  >
                                    {browserPath || "/"}
                                  </Typography>
                                </Box>
                                {browserPath && (
                                  <Button
                                    size="small"
                                    startIcon={<ArrowUpward />}
                                    onClick={handleBrowserUp}
                                    disabled={creating}
                                  >
                                    {t("wizard.up")}
                                  </Button>
                                )}
                              </Stack>
                              {browserLoading ? (
                                <Box
                                  display="flex"
                                  justifyContent="center"
                                  p={2}
                                >
                                  <CircularProgress size={24} />
                                </Box>
                              ) : browserDirs.length === 0 ? (
                                <Typography
                                  variant="body2"
                                  color="text.secondary"
                                  textAlign="center"
                                  py={2}
                                >
                                  {browserPath
                                    ? t("wizard.noSubdirectories")
                                    : t("wizard.clickToLoad")}
                                </Typography>
                              ) : (
                                <List dense>
                                  {browserDirs.map((dir) => (
                                    <ListItem key={dir} disablePadding>
                                      <ListItemButton
                                        onClick={() =>
                                          handleBrowserNavigate(dir)
                                        }
                                        disabled={creating}
                                      >
                                        <ListItemIcon sx={{ minWidth: 36 }}>
                                          <Folder />
                                        </ListItemIcon>
                                        <ListItemText primary={dir} />
                                      </ListItemButton>
                                    </ListItem>
                                  ))}
                                </List>
                              )}
                            </Stack>
                          </Paper>
                        )}
                      </>
                    )}
                  </Stack>
                </CardContent>
              </Card>

              <Stack spacing={2}>
                {testMessage && (
                  <Alert severity="success" icon={<CheckCircle />}>
                    {testMessage}
                  </Alert>
                )}
                {testError && <Alert severity="error">{testError}</Alert>}
                <Stack direction="row" spacing={2} justifyContent="flex-end">
                  <Button
                    variant="outlined"
                    onClick={handleTest}
                    disabled={testLoading || creating}
                  >
                    {testLoading
                      ? t("wizard.testing")
                      : t("wizard.testConnection")}
                  </Button>
                  <Button
                    type="submit"
                    variant="contained"
                    disabled={!testMessage || creating}
                    startIcon={creating ? <CircularProgress size={20} /> : null}
                  >
                    {creating ? translations.creating : translations.create}
                  </Button>
                </Stack>
              </Stack>
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
        <Alert
          severity="success"
          icon={<CheckCircle />}
          sx={{ width: "100%" }}
        >
          {translations.createSuccess}
        </Alert>
      </Snackbar>
    </>
  );
}
