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
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ArrowBack } from "@mui/icons-material";
import type { BackupStorage } from "../types/api";
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
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    console.log("Create storage", { typeId, parameters: params });
    alert(t("storageWizard.storageCreated"));
    navigate("/storages");
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
                    storageMeta.parameters.map((p) => (
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
                    ))
                  )}
                </Stack>
              </CardContent>
            </Card>

            <Stack direction="row" spacing={2}>
              <Button type="submit" variant="contained">
                {t("storageWizard.createStorage")}
              </Button>
              <Button variant="outlined" onClick={() => navigate("/storages")}>
                {t("storageWizard.cancel")}
              </Button>
            </Stack>
          </Stack>
        </Box>
      )}
    </Stack>
  );
}
