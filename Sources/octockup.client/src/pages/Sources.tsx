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
import type { BackupSource, UserBackupSource } from "../types/api";
import { AddCircleOutline } from "@mui/icons-material";
import { getSourceIcon } from "../constants/sourceIcons";
import { useBackupSourcesApi } from "../api/backupSourcesApi";

interface State {
  loading: boolean;
  error: string | null;
  availableLoading: boolean;
  availableError: string | null;
}

export function SourcesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useBackupSourcesApi();
  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    availableLoading: true,
    availableError: null,
  });
  const [userSources, setUserSources] = useState<UserBackupSource[]>([]);
  const [availableSources, setAvailableSources] = useState<BackupSource[]>([]);

  useEffect(() => {
    let active = true;

    // Load user's created sources
    api
      .list()
      .then((data) => {
        if (!active) return;
        setUserSources(data);
        setState((prev) => ({ ...prev, loading: false, error: null }));
      })
      .catch((e) => {
        if (!active) return;
        setState((prev) => ({
          ...prev,
          loading: false,
          error: e?.message || "Failed to load sources",
        }));
      });

    // Load available source types
    api
      .listAvailable()
      .then((data) => {
        if (!active) return;
        setAvailableSources(data);
        setState((prev) => ({
          ...prev,
          availableLoading: false,
          availableError: null,
        }));
      })
      .catch((e) => {
        if (!active) return;
        setState((prev) => ({
          ...prev,
          availableLoading: false,
          availableError: e?.message || "Failed to load available sources",
        }));
      });

    return () => {
      active = false;
    };
  }, [api]);

  if (state.loading || state.availableLoading) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }

  if (state.error || state.availableError) {
    return (
      <Box p={2}>
        <Alert severity="error">{state.error || state.availableError}</Alert>
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h5" gutterBottom>
          {t("sources.title")}
        </Typography>
        {userSources.length === 0 ? (
          <Card variant="outlined">
            <CardContent>
              <Typography color="text.secondary">
                {t("sources.noSources")}
              </Typography>
            </CardContent>
          </Card>
        ) : (
          <Stack direction="row" spacing={2} flexWrap="wrap">
            {userSources.map((s) => (
              <Card
                key={s.tag}
                sx={{
                  width: 160,
                  height: 140,
                  flex: "0 0 160px",
                  display: "flex",
                  alignItems: "stretch",
                  justifyContent: "center",
                }}
              >
                <CardContent
                  sx={{
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    gap: 0.5,
                    justifyContent: "space-between",
                    height: "100%",
                    p: 2,
                  }}
                >
                  <Box sx={{ fontSize: 32 }}>{getSourceIcon(s.backupSourceId)}</Box>
                  <Typography
                    variant="subtitle2"
                    noWrap
                    title={s.tag}
                    sx={{ textAlign: "center", maxWidth: 140 }}
                  >
                    {s.tag}
                  </Typography>
                  <Typography
                    variant="caption"
                    sx={{ textAlign: "center", maxWidth: 140, color: "text.secondary", fontSize: "0.7rem" }}
                  >
                    {new Date(s.createdAt).toLocaleDateString()}
                  </Typography>
                </CardContent>
              </Card>
            ))}
          </Stack>
        )}
      </Box>
      <Divider />
      <Box>
        <Typography variant="h6" gutterBottom>
          {t("sources.addNew")}
        </Typography>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          {availableSources.map((s) => (
            <Card
              key={s.id}
              sx={{
                width: 160,
                height: 140,
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
                  gap: 0.5,
                  justifyContent: "space-between",
                  height: "100%",
                  p: 2,
                }}
              >
                <Box sx={{ fontSize: 32 }}>{getSourceIcon(s.id)}</Box>
                <Typography
                  variant="caption"
                  noWrap
                  sx={{ textAlign: "center", maxWidth: 140 }}
                >
                  {s.name}
                </Typography>
              </CardContent>
            </Card>
          ))}
          {availableSources.length === 0 && (
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
