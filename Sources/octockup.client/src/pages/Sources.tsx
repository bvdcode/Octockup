import {
  Box,
  Card,
  Stack,
  Alert,
  Divider,
  Typography,
  CardContent,
  CircularProgress,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { BackupSource } from "../types/api";
import { AddCircleOutline } from "@mui/icons-material";
import { getSourceIcon } from "../constants/sourceIcons";
import { useBackupSourcesApi } from "../api/backupSourcesApi";

interface State {
  loading: boolean;
  error: string | null;
}

export function SourcesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useBackupSourcesApi();
  const [state, setState] = useState<State>({ loading: true, error: null });
  const [sources, setSources] = useState<BackupSource[]>([]);

  useEffect(() => {
    let active = true;
    api
      .list()
      .then((data) => {
        if (!active) return;
        setSources(data);
        setState({ loading: false, error: null });
      })
      .catch((e) => {
        if (!active) return;
        setState({
          loading: false,
          error: e?.message || "Failed to load sources",
        });
      });
    return () => {
      active = false;
    };
  }, [api]);

  if (state.loading) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }

  if (state.error) {
    return (
      <Box p={2}>
        <Alert severity="error">{state.error}</Alert>
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h5" gutterBottom>
          {t("sources.title")}
        </Typography>
        <Card variant="outlined">
          <CardContent>
            <Typography color="text.secondary">
              {t("sources.noSources")}
            </Typography>
          </CardContent>
        </Card>
      </Box>
      <Divider />
      <Box>
        <Typography variant="h6" gutterBottom>
          {t("sources.addNew")}
        </Typography>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          {sources.map((s) => (
            <Card
              key={s.id}
              sx={{
                width: 160,
                height: 120,
                flex: "0 0 160px",
                cursor: "pointer",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                "&:hover": { bgcolor: "action.hover" },
              }}
              onClick={() => {
                navigate(`/sources/new?type=${encodeURIComponent(s.id)}`);
              }}
            >
              <CardContent
                sx={{
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  gap: 1,
                  justifyContent: "center",
                  height: "100%",
                }}
              >
                <Box sx={{ fontSize: 32 }}>{getSourceIcon(s.id)}</Box>
                <Typography variant="caption" noWrap sx={{ textAlign: "center", maxWidth: 140 }}>
                  {s.name}
                </Typography>
              </CardContent>
            </Card>
          ))}
          {sources.length === 0 && (
            <Stack
              direction="row"
              spacing={1}
              alignItems="center"
              color="text.secondary"
            >
              <AddCircleOutline />
              <Typography variant="body2">
                {t("sources.noTypesAvailable")}
              </Typography>
            </Stack>
          )}
        </Stack>
      </Box>
    </Stack>
  );
}

export default SourcesPage;
