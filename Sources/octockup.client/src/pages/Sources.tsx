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
import {
  useState,
  useEffect,
  type ChangeEvent,
  type KeyboardEvent,
} from "react";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { ModuleDestination } from "../types/api";
import { parseUtcDate } from "../utils/dateUtils";
import { useModulesApi } from "../api/modulesApi";
import { DeleteOutline } from "@mui/icons-material";
import { AddCircleOutline } from "@mui/icons-material";
import { getSourceIcon } from "../constants/sourceIcons";
import type { Module, ModuleProviderInfo } from "../types/api";

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
  const [availableSources, setAvailableSources] = useState<
    ModuleProviderInfo[]
  >([]);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingTag, setEditingTag] = useState<string>("");
  const [snackbar, setSnackbar] = useState<string | null>(null);

  const handleDoubleClick = (module: Module) => {
    setEditingId(module.id);
    setEditingTag(module.tag);
  };

  const handleRename = async (moduleId: string) => {
    if (
      !editingTag.trim() ||
      editingTag === userSources.find((s) => s.id === moduleId)?.tag
    ) {
      setEditingId(null);
      return;
    }
    try {
      await api.rename(moduleId, editingTag.trim());
      setUserSources((prev) =>
        prev.map((s) =>
          s.id === moduleId ? { ...s, tag: editingTag.trim() } : s,
        ),
      );
      setEditingId(null);
    } catch (e) {
      const error = e as { response?: { data?: { message?: string } } };
      setSnackbar(error?.response?.data?.message || "Failed to rename");
      setEditingId(null);
    }
  };

  useEffect(() => {
    let active = true;

    // Load user's created sources
    api
      .list()
      .then((data) => {
        if (!active) return;
        // filter sources
        setUserSources(
          data.filter((m) => m.destination === ModuleDestination.Source),
        );
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
      .listProvidersByType("source")
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
          <Card>
            <CardContent>
              <Typography color="text.secondary">
                {t("sources.noSources")}
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
            {userSources.map((s) => (
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
                <Tooltip title={t("sources.deleteTooltip")} placement="left">
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
                        title: t("sources.deleteTitle"),
                        description: t("sources.deleteText"),
                        confirmationText: t("common.delete"),
                        cancellationText: t("common.cancel"),
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
                  {editingId === s.id ? (
                    <Box
                      component="input"
                      autoFocus
                      value={editingTag}
                      onChange={(e: ChangeEvent<HTMLInputElement>) =>
                        setEditingTag(e.target.value)
                      }
                      onBlur={() => handleRename(s.id)}
                      onKeyDown={(e: KeyboardEvent<HTMLInputElement>) => {
                        if (e.key === "Enter") handleRename(s.id);
                        if (e.key === "Escape") setEditingId(null);
                      }}
                      sx={{
                        textAlign: "center",
                        maxWidth: 140,
                        fontSize: "0.875rem",
                        fontWeight: 500,
                        border: "1px solid",
                        borderColor: "primary.main",
                        borderRadius: 1,
                        px: 0.5,
                        py: 0.25,
                        outline: "none",
                      }}
                    />
                  ) : (
                    <Typography
                      variant="subtitle2"
                      noWrap
                      title={s.tag}
                      sx={{
                        textAlign: "center",
                        maxWidth: 140,
                        cursor: "text",
                      }}
                      onDoubleClick={(e) => {
                        e.stopPropagation();
                        handleDoubleClick(s);
                      }}
                    >
                      {s.tag}
                    </Typography>
                  )}
                  <Typography
                    variant="caption"
                    sx={{
                      textAlign: "center",
                      maxWidth: 140,
                      color: "text.secondary",
                      fontSize: "0.7rem",
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
          {t("sources.addNew")}
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
          {availableSources.map((s) => (
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

export default SourcesPage;
