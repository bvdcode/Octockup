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

  function updateParam(name: string, value: string) {
    setParams((prev) => ({ ...prev, [name]: value }));
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    console.log("Create source", { typeId, parameters: params });
    alert(t("wizard.sourceCreated"));
    navigate("/sources");
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
        <Box component="form" onSubmit={handleSubmit} maxWidth={640}>
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
                    sourceMeta.parameters.map((p) => (
                      <TextField
                        key={p}
                        required
                        fullWidth
                        label={p}
                        value={params[p] ?? ""}
                        onChange={(e) => updateParam(p, e.target.value)}
                        placeholder={t("wizard.enterValue", { param: p })}
                      />
                    ))
                  )}
                </Stack>
              </CardContent>
            </Card>

            <Stack direction="row" spacing={2}>
              <Button type="submit" variant="contained">
                {t("wizard.createSource")}
              </Button>
              <Button variant="outlined" onClick={() => navigate("/sources")}>
                {t("wizard.cancel")}
              </Button>
            </Stack>
          </Stack>
        </Box>
      )}
    </Stack>
  );
}
