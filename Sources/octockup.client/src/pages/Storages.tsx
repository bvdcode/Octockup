import {
  Box,
  Card,
  Stack,
  Alert,
  Divider,
  Tooltip,
  Snackbar,
  IconButton,
  Typography,
  CardContent,
  CircularProgress,
} from "@mui/material";
import { useState, useEffect } from "react";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { ModuleDestination } from "../types/api";
import { useModulesApi } from "../api/modulesApi";
import { parseUtcDate } from "../utils/dateUtils";
import { getSourceIcon } from "../constants/sourceIcons";
import type { Module, ModuleProviderInfo } from "../types/api";
import { AddCircleOutline, DeleteOutline } from "@mui/icons-material";
import { EditableModuleTag } from "../components/EditableModuleTag";

interface State {
  loading: boolean;
  error: string | null;
  availableLoading: boolean;
  availableError: string | null;
}

export function StoragesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useModulesApi();
  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    availableLoading: true,
    availableError: null,
  });
  const [userStorages, setUserStorages] = useState<Module[]>([]);
  const [availableStorages, setAvailableStorages] = useState<
    ModuleProviderInfo[]
  >([]);
  const [snackbar, setSnackbar] = useState<string | null>(null);

  const handleRename = async (moduleId: string, newTag: string) => {
    try {
      await api.rename(moduleId, newTag);
      setUserStorages((prev) =>
        prev.map((s) => (s.id === moduleId ? { ...s, tag: newTag } : s)),
      );
    } catch (e) {
      const error = e as { response?: { data?: { message?: string } } };
      setSnackbar(error?.response?.data?.message || "Failed to rename");
      throw e;
    }
  };

  useEffect(() => {
    let active = true;
    // load user-created storages
    api
      .list()
      .then((data) => {
        if (!active) return;
        setUserStorages(
          data.filter((m) => m.destination === ModuleDestination.Target),
        );
        setState((prev) => ({ ...prev, loading: false, error: null }));
      })
      .catch((e) => {
        if (!active) return;
        setState((prev) => ({
          ...prev,
          loading: false,
          error: e?.message || "Failed to load storages",
        }));
      });

    // load available storage types
    api
      .listProvidersByType("storage")
      .then((data) => {
        if (!active) return;
        setAvailableStorages(data);
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
          availableError: e?.message || "Failed to load available storages",
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
        <Alert severity="error">{state.error}</Alert>
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h5" gutterBottom>
          {t("storages.title")}
        </Typography>
        {userStorages.length === 0 ? (
          <Card>
            <CardContent>
              <Typography color="text.secondary">
                {t("storages.noStorages")}
              </Typography>
            </CardContent>
          </Card>
        ) : (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "repeat(auto-fill, minmax(140px, 1fr))",
                sm: "repeat(auto-fill, minmax(160px, 1fr))",
              },
              gap: 2,
            }}
          >
            {userStorages.map((s) => (
              <Card
                key={s.tag}
                sx={{
                  height: 140,
                  display: "flex",
                  alignItems: "stretch",
                  justifyContent: "center",
                  position: "relative",
                }}
              >
                <Tooltip title={t("storages.deleteTooltip")} placement="left">
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
                        title: t("storages.deleteTitle"),
                        description: t("storages.deleteText"),
                        confirmationText: t("common.delete"),
                        cancellationText: t("common.cancel"),
                        confirmationButtonProps: { color: "error" },
                      });
                      if (result.confirmed) {
                        await api.delete(s.id);
                        setUserStorages((prev) =>
                          prev.filter((x) => x.id !== s.id),
                        );
                      }
                    }}
                  >
                    <DeleteOutline fontSize="small" color="primary" />
                  </IconButton>
                </Tooltip>
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
                  <EditableModuleTag
                    tag={s.tag}
                    onRename={(newTag) => handleRename(s.id, newTag)}
                  />
                  <Typography
                    variant="caption"
                    noWrap
                    sx={{
                      textAlign: "center",
                      maxWidth: 140,
                      fontSize: "0.7rem",
                      color: "text.secondary",
                    }}
                  >
                    {parseUtcDate(s.createdAt)!.toLocaleDateString()}
                  </Typography>
                </CardContent>
              </Card>
            ))}
          </Box>
        )}
      </Box>
      <Divider />
      <Box>
        <Typography variant="h6" gutterBottom>
          {t("storages.addNew")}
        </Typography>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "repeat(auto-fill, minmax(140px, 1fr))",
              sm: "repeat(auto-fill, minmax(160px, 1fr))",
            },
            gap: 2,
          }}
        >
          {availableStorages.map((s) => (
            <Card
              key={s.id}
              sx={{
                height: 140,
                cursor: "pointer",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                "&:hover": { bgcolor: "action.hover" },
              }}
              onClick={() => {
                navigate(`/storages/new?type=${encodeURIComponent(s.id)}`);
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
          {availableStorages.length === 0 && (
            <Stack
              direction="row"
              spacing={1}
              alignItems="center"
              color="text.secondary"
            >
              <AddCircleOutline />
              <Typography variant="body2">
                {t("storages.noTypesAvailable")}
              </Typography>
            </Stack>
          )}
        </Box>
      </Box>
      <Snackbar
        open={snackbar !== null}
        autoHideDuration={6000}
        onClose={() => setSnackbar(null)}
        message={snackbar}
      />
    </Stack>
  );
}

export default StoragesPage;
