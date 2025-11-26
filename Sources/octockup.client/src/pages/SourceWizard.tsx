import {
  Box,
  List,
  Card,
  Paper,
  Alert,
  Stack,
  Button,
  ListItem,
  TextField,
  Typography,
  IconButton,
  CardContent,
  ListItemIcon,
  ListItemText,
  ListItemButton,
  CircularProgress,
} from "@mui/material";
import {
  Home,
  Folder,
  ArrowBack,
  ArrowRight,
  ArrowUpward,
} from "@mui/icons-material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { BackupSource } from "../types/api";
import { getSourceIcon } from "../constants/sourceIcons";
import { useBackupSourcesApi } from "../api/backupSourcesApi";
import { useNavigate, useSearchParams } from "react-router-dom";

interface ParamState {
  [key: string]: string;
}

export default function SourceWizard() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useBackupSourcesApi();
  const [searchParams] = useSearchParams();
  const typeId = searchParams.get("type") || "";
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [sourceMeta, setSourceMeta] = useState<BackupSource | null>(null);
  const [params, setParams] = useState<ParamState>({});
  const [testLoading, setTestLoading] = useState(false);
  const [testMessage, setTestMessage] = useState<string | null>(null);
  const [testError, setTestError] = useState<string | null>(null);
  const [browserPath, setBrowserPath] = useState<string>("");
  const [browserDirs, setBrowserDirs] = useState<string[]>([]);
  const [browserLoading, setBrowserLoading] = useState(false);

  useEffect(() => {
    let active = true;

    const load = async () => {
      if (!typeId) {
        setError(t("wizard.typeNotSpecified"));
        setLoading(false);
        return;
      }

      try {
        const all = await api.list();
        if (!active) return;

        const meta = all.find((x) => x.id === typeId);
        if (!meta) {
          setError(t("wizard.typeNotFound"));
        } else {
          setSourceMeta(meta);
          const initial: ParamState = {};
          meta.parameters.forEach((p) => (initial[p] = ""));
          setParams(initial);
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
  }, [api, typeId, t]);

  useEffect(() => {
    if (!sourceMeta || !sourceMeta.parameters.includes("path")) return;
    const required = sourceMeta.parameters.filter((p) => p !== "path");
    const allFilled = required.every(
      (p) => params[p] && String(params[p]).length > 0,
    );
    const sep = sourceMeta.pathSeparator || "/";
    if (
      allFilled &&
      browserPath === "" &&
      browserDirs.length === 0 &&
      !browserLoading
    ) {
      loadBrowserDirectories(sep);
    }
  }, [params, sourceMeta, browserPath, browserDirs.length, browserLoading]);

  function updateParam(name: string, value: string) {
    setParams((prev) => ({ ...prev, [name]: value }));
    setTestMessage(null);
    setTestError(null);
    if (name !== "path" && sourceMeta?.parameters.includes("path")) {
      setBrowserPath("");
      setBrowserDirs([]);
    }
  }

  async function loadBrowserDirectories(targetPath: string) {
    if (!sourceMeta || browserLoading) return;
    const required = sourceMeta.parameters.filter((p) => p !== "path");
    const missing = required.filter(
      (p) => !(params[p] && String(params[p]).length > 0),
    );
    if (missing.length > 0) {
      return;
    }
    try {
      setBrowserLoading(true);
      const paramsWithPath = { ...params, path: targetPath };
      const dirs = await api.getDirectories(typeId, paramsWithPath);
      setBrowserDirs(dirs || []);
      setBrowserPath(targetPath);
      // Update path field with current browser path
      setParams((prev) => ({ ...prev, path: targetPath }));
      // Reset test results when path changes
      setTestMessage(null);
      setTestError(null);
    } catch {
      setBrowserDirs([]);
    } finally {
      setBrowserLoading(false);
    }
  }

  function handleBrowserNavigate(dir: string) {
    const sep = sourceMeta?.pathSeparator || "/";
    const newPath =
      browserPath === sep
        ? `${browserPath}${dir}`
        : `${browserPath}${sep}${dir}`;
    loadBrowserDirectories(newPath);
  }

  function handleBrowserUp() {
    const sep = sourceMeta?.pathSeparator || "/";
    const lastSepIndex = browserPath.lastIndexOf(sep);
    const newPath =
      lastSepIndex <= 0 ? sep : browserPath.substring(0, lastSepIndex);
    loadBrowserDirectories(newPath);
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    alert(t("wizard.sourceCreated"));
    navigate("/sources");
  }

  async function handleTest() {
    setTestError(null);
    setTestMessage(null);
    if (!sourceMeta) return;
    // Ensure all parameters are filled
    const required = sourceMeta.parameters || [];
    const missing = required.filter(
      (p) => !(params[p] && String(params[p]).length > 0),
    );
    if (missing.length > 0) {
      setTestError(t("wizard.fillParameters"));
      return;
    }
    try {
      setTestLoading(true);
      const result = await api.test(typeId, params);
      if (Array.isArray(result)) {
        setTestMessage(t("wizard.testSuccess"));
      } else if (result && Array.isArray(result.items)) {
        setTestMessage(t("wizard.testSuccess"));
      } else {
        const message = result?.message ?? t("wizard.testSuccess");
        setTestMessage(
          typeof message === "string" ? message : JSON.stringify(message),
        );
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      setTestError(msg || t("wizard.testFailed"));
    } finally {
      setTestLoading(false);
    }
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
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      <Stack direction="row" spacing={2} alignItems="center">
        <Button
          variant="outlined"
          startIcon={<ArrowBack />}
          onClick={() => navigate("/sources")}
        >
          {t("wizard.back")}
        </Button>
        <Typography variant="h5">{t("wizard.title")}</Typography>
      </Stack>
      {sourceMeta && (
        <Box component="form" onSubmit={handleSubmit}>
          <Stack spacing={3}>
            <Card variant="outlined">
              <CardContent>
                <Stack direction="row" spacing={2} alignItems="center">
                  <Box sx={{ fontSize: 42 }}>
                    {getSourceIcon(sourceMeta.id)}
                  </Box>
                  <Box>
                    <Typography variant="h6">{sourceMeta.name}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {sourceMeta.id}
                    </Typography>
                  </Box>
                </Stack>
              </CardContent>
            </Card>

            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle1" gutterBottom>
                  {t("wizard.parameters")}
                </Typography>
                <Stack spacing={2}>
                  {sourceMeta.parameters.length === 0 ? (
                    <Typography
                      variant="body2"
                      color="text.secondary"
                      fontStyle="italic"
                    >
                      {t("wizard.noParameters")}
                    </Typography>
                  ) : (
                    <>
                      {sourceMeta.parameters.map((p) => (
                        <TextField
                          key={p}
                          required={p !== "path"}
                          fullWidth
                          label={p}
                          value={params[p] ?? ""}
                          onChange={(e) => updateParam(p, e.target.value)}
                          placeholder={t("wizard.enterValue", { param: p })}
                        />
                      ))}
                      {sourceMeta.parameters.includes("path") && (
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
                                      sourceMeta?.pathSeparator || "/";
                                    loadBrowserDirectories(sep);
                                  }}
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
                                >
                                  {t("wizard.up")}
                                </Button>
                              )}
                            </Stack>
                            {browserLoading ? (
                              <Box display="flex" justifyContent="center" p={2}>
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
                                      onClick={() => handleBrowserNavigate(dir)}
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
              {testMessage && <Alert severity="success">{testMessage}</Alert>}
              {testError && <Alert severity="error">{testError}</Alert>}
              <Stack direction="row" spacing={2} justifyContent="flex-end">
                <Button
                  variant="outlined"
                  onClick={handleTest}
                  disabled={testLoading}
                >
                  {testLoading
                    ? t("wizard.testing")
                    : t("wizard.testConnection")}
                </Button>
                <Button
                  type="submit"
                  variant="contained"
                  disabled={!testMessage}
                >
                  {t("wizard.createSource")}
                </Button>
              </Stack>
            </Stack>
          </Stack>
        </Box>
      )}
    </Stack>
  );
}
