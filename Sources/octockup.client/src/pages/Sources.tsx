import {
  Box,
  Card,
  Stack,
  Alert,
  Divider,
  Typography,
  CardContent,
  CircularProgress,
  IconButton,
} from "@mui/material";
import { useEffect, useState } from "react";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { AddCircleOutline } from "@mui/icons-material";
import { DeleteOutline } from "@mui/icons-material";
import { getSourceIcon } from "../constants/sourceIcons";
import { useModulesApi } from "../api/modulesApi";
import type { Module, ModuleProviderInfo, ModuleDestination } from "../types/api";

interface State {
  loading: boolean;
  error: string | null;
  availableLoading: boolean;
  availableError: string | null;
}

export function SourcesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useModulesApi();
  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    availableLoading: true,
    availableError: null,
  });
  const [userSources, setUserSources] = useState<Module[]>([]);
  const [availableSources, setAvailableSources] = useState<ModuleProviderInfo[]>([]);

  useEffect(() => {
    let active = true;

    // Load user's created sources
    api
      .list()
      .then((data) => {
        if (!active) return;
        // filter sources
        setUserSources(data.filter((m) => m.Type === ModuleDestination.Source));
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
      .listProviders()
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
                  position: "relative",
                }}
              >
                <IconButton
                  size="small"
                  aria-label={t("common.delete")}
                  sx={{
                    position: "absolute",
                    top: 4,
                    right: 4,
                  }}
                  onClick={async (e) => {
                    e.stopPropagation();
                    const result = await confirm({
                      title: t("sources.deleteTitle", {
                        defaultValue: "Delete source",
                      }),
                      description: t("sources.deleteText", {
                        defaultValue: "This action is permanent!",
                      }),
                      confirmationText: t("common.delete", {
                        defaultValue: "Delete",
                      }),
                      cancellationText: t("common.cancel", {
                        defaultValue: "Cancel",
                      }),
                      confirmationButtonProps: { color: "error" },
                    });
                    if (result.confirmed) {
                      await api.delete(s.id);
                      setUserSources((prev) =>
                        prev.filter((x) => x.id !== s.id),
                      );
                    }
                  }}
                >
                  <DeleteOutline fontSize="small" color="primary" />
                </IconButton>
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
                  <Box sx={{ fontSize: 32 }}>
                    {getSourceIcon(s.backupModuleId)}
                  </Box>
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
                    sx={{
                      textAlign: "center",
                      maxWidth: 140,
                      color: "text.secondary",
                      fontSize: "0.7rem",
                    }}
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
