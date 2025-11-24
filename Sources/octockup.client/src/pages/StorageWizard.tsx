import {
  Box,
  Card,
  Alert,
  Stack,
  Button,
  TextField,
  Typography,
  CardContent,
  CircularProgress,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Paper,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ArrowBack, Folder, ArrowUpward } from "@mui/icons-material";
import type { BackupStorage, TestResultItem } from "../types/api";
import { getSourceIcon } from "../constants/sourceIcons";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useBackupStoragesApi } from "../api/backupStoragesApi";

interface ParamState {
  [key: string]: string;
}

export default function StorageWizard() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useBackupStoragesApi();
  const [searchParams] = useSearchParams();
  const typeId = searchParams.get("type") || "";
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [storageMeta, setStorageMeta] = useState<BackupStorage | null>(null);
  const [params, setParams] = useState<ParamState>({});
  const [testLoading, setTestLoading] = useState(false);
  const [testMessage, setTestMessage] = useState<string | null>(null);
  const [testError, setTestError] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<TestResultItem[] | null>(null);
  const [browserPath, setBrowserPath] = useState<string>("");
  const [browserDirs, setBrowserDirs] = useState<string[]>([]);
  const [browserLoading, setBrowserLoading] = useState(false);

  useEffect(() => {
    let active = true;

    const load = async () => {
      if (!typeId) {
        setError(t("storageWizard.typeNotSpecified"));
        setLoading(false);
        return;
      }

      try {
        const all = await api.list();
        if (!active) return;

        const meta = all.find((x) => x.id === typeId);
        if (!meta) {
          setError(t("storageWizard.typeNotFound"));
        } else {
          setStorageMeta(meta);
          const initial: ParamState = {};
          meta.parameters.forEach((p) => (initial[p] = ""));
          setParams(initial);
        }
        setLoading(false);
      } catch (e: unknown) {
        if (!active) return;
        setError(e instanceof Error ? e.message : t("storageWizard.loadError"));
        setLoading(false);
      }
    };

    load();

    return () => {
      active = false;
    };
  }, [api, typeId, t]);

  function updateParam(name: string, value: string) {
    setParams((prev) => ({ ...prev, [name]: value }));
    setTestMessage(null);
    setTestError(null);
    setTestResult(null);
    // If user changes any param (except path), reset browser
    if (name !== "path" && storageMeta?.parameters.includes("path")) {
      setBrowserPath("");
      setBrowserDirs([]);
    }
  }

  async function loadBrowserDirectories(targetPath: string) {
    if (!storageMeta || browserLoading) return;
    const required = storageMeta.parameters.filter((p) => p !== "path");
    const missing = required.filter((p) => !(params[p] && String(params[p]).length > 0));
    if (missing.length > 0) {
      return;
    }
    try {
      setBrowserLoading(true);
      const paramsWithPath = { ...params, path: targetPath };
      const dirs = await api.getDirectories(typeId, paramsWithPath);
      setBrowserDirs(dirs || []);
      setBrowserPath(targetPath);
    } catch (err: unknown) {
      console.error("Failed to load directories:", err);
      setBrowserDirs([]);
    } finally {
      setBrowserLoading(false);
    }
  }

  function handleBrowserNavigate(dir: string) {
    const newPath = browserPath ? `${browserPath}/${dir}` : dir;
    loadBrowserDirectories(newPath);
  }

  function handleBrowserUp() {
    const parts = browserPath.split("/").filter(Boolean);
    parts.pop();
    const newPath = parts.join("/");
    loadBrowserDirectories(newPath);
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    alert(t("storageWizard.storageCreated"));
    navigate("/storages");
  }

  async function handleTest() {
    setTestError(null);
    setTestMessage(null);
    setTestResult(null);
    if (!storageMeta) return;
    const required = storageMeta.parameters || [];
    const missing = required.filter((p) => !(params[p] && String(params[p]).length > 0));
    if (missing.length > 0) {
      setTestError(t("wizard.fillParameters"));
      return;
    }
    try {
      setTestLoading(true);
      const result = await api.test(typeId, params);
      if (Array.isArray(result)) {
        setTestResult(result as TestResultItem[]);
        setTestMessage(t("storageWizard.testSuccess") ?? t("wizard.testSuccess"));
      } else if (result && Array.isArray(result.items)) {
        setTestResult(result.items as TestResultItem[]);
        setTestMessage(t("storageWizard.testSuccess") ?? t("wizard.testSuccess"));
      } else {
        const message = result?.message ?? t("storageWizard.testSuccess") ?? t("wizard.testSuccess");
        setTestMessage(typeof message === "string" ? message : JSON.stringify(message));
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      setTestError(msg || (t("storageWizard.testFailed") ?? t("wizard.testFailed")));
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

  function humanBytes(bytes: number | null | undefined) {
    if (bytes == null) return "-";
    if (bytes === 0) return "0 B";
    const sizes = ["B", "KB", "MB", "GB", "TB"];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return `${(bytes / Math.pow(1024, i)).toFixed(2)} ${sizes[i]}`;
  }

  function formatLocalWithMs(utcString?: string | null) {
    if (!utcString) return "-";
    const d = new Date(utcString);
    if (isNaN(d.getTime())) return utcString;
    const datePart = d.toLocaleDateString();
    const timePart = d.toLocaleTimeString(undefined, {
      hour12: false,
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
    const ms = d.getMilliseconds().toString().padStart(3, "0");
    return `${datePart} ${timePart}.${ms}`;
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
          onClick={() => navigate("/storages")}
        >
          {t("storageWizard.back")}
        </Button>
        <Typography variant="h5">{t("storageWizard.title")}</Typography>
      </Stack>
      {storageMeta && (
        <Box component="form" onSubmit={handleSubmit}>
          <Stack spacing={3}>
            <Card variant="outlined">
              <CardContent>
                <Stack direction="row" spacing={2} alignItems="center">
                  <Box sx={{ fontSize: 42 }}>
                    {getSourceIcon(storageMeta.id)}
                  </Box>
                  <Box>
                    <Typography variant="h6">{storageMeta.name}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {storageMeta.id}
                    </Typography>
                  </Box>
                </Stack>
              </CardContent>
            </Card>

            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle1" gutterBottom>
                  {t("storageWizard.parameters")}
                </Typography>
                <Stack spacing={2}>
                  {storageMeta.parameters.length === 0 ? (
                    <Typography
                      variant="body2"
                      color="text.secondary"
                      fontStyle="italic"
                    >
                      {t("storageWizard.noParameters")}
                    </Typography>
                  ) : (
                    <>
                      {storageMeta.parameters.map((p) => (
                        <TextField
                          key={p}
                          required
                          fullWidth
                          label={p}
                          value={params[p] ?? ""}
                          onChange={(e) => updateParam(p, e.target.value)}
                          placeholder={t("storageWizard.enterValue", {
                            param: p,
                          })}
                        />
                      ))}
                      {storageMeta.parameters.includes("path") && (
                        <Paper variant="outlined" sx={{ p: 2, maxHeight: 300, overflow: "auto" }}>
                          <Stack spacing={1}>
                            <Stack direction="row" alignItems="center" justifyContent="space-between">
                              <Typography variant="caption" color="text.secondary">
                                {t("wizard.directoryBrowser")}: /{browserPath}
                              </Typography>
                              {browserPath && (
                                <Button size="small" startIcon={<ArrowUpward />} onClick={handleBrowserUp}>
                                  {t("wizard.up")}
                                </Button>
                              )}
                            </Stack>
                            {browserLoading ? (
                              <Box display="flex" justifyContent="center" p={2}>
                                <CircularProgress size={24} />
                              </Box>
                            ) : browserDirs.length === 0 ? (
                              <Typography variant="body2" color="text.secondary" textAlign="center" py={2}>
                                {browserPath ? t("wizard.noSubdirectories") : t("wizard.clickToLoad")}
                              </Typography>
                            ) : (
                              <List dense>
                                {browserDirs.map((dir) => (
                                  <ListItem key={dir} disablePadding>
                                    <ListItemButton onClick={() => handleBrowserNavigate(dir)}>
                                      <ListItemIcon sx={{ minWidth: 36 }}>
                                        <Folder />
                                      </ListItemIcon>
                                      <ListItemText primary={dir} />
                                    </ListItemButton>
                                  </ListItem>
                                ))}
                              </List>
                            )}
                            {!browserPath && !browserLoading && (
                              <Button
                                size="small"
                                variant="outlined"
                                onClick={() => loadBrowserDirectories("")}
                                fullWidth
                              >
                                {t("wizard.loadRootDirectories")}
                              </Button>
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
              {testResult && (
                <Card variant="outlined">
                  <CardContent>
                    <Typography variant="subtitle2" gutterBottom>
                      {t("wizard.testResult")}
                    </Typography>
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell>{t("wizard.fileName")}</TableCell>
                          <TableCell>{t("wizard.filePath")}</TableCell>
                          <TableCell>{t("wizard.fileSize")}</TableCell>
                          <TableCell>{t("wizard.fileModified")}</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {testResult.map((it) => (
                          <TableRow key={it.path + it.name}>
                            <TableCell>{it.name}</TableCell>
                            <TableCell>{it.path}</TableCell>
                            <TableCell>{humanBytes(it.size)}</TableCell>
                            <TableCell>{formatLocalWithMs(it.lastModified)}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </CardContent>
                </Card>
              )}
              <Stack direction="row" spacing={2}>
                <Button type="submit" variant="contained">
                  {t("storageWizard.createStorage")}
                </Button>
                <Button variant="outlined" onClick={() => navigate("/storages") }>
                  {t("storageWizard.cancel")}
                </Button>
                <Button variant="outlined" onClick={handleTest} disabled={testLoading}>
                  {testLoading ? t("wizard.testing") : t("wizard.testConnection")}
                </Button>
              </Stack>
            </Stack>
          </Stack>
        </Box>
      )}
    </Stack>
  );
}
