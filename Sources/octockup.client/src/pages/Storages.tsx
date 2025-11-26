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
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { BackupStorage, SavedBackupModule } from "../types/api";
import { AddCircleOutline, DeleteOutline } from "@mui/icons-material";
import { confirm } from "material-ui-confirm";
import { getSourceIcon } from "../constants/sourceIcons";
import { useBackupStoragesApi } from "../api/backupStoragesApi";

interface State {
  loading: boolean;
  error: string | null;
  availableLoading: boolean;
  availableError: string | null;
}

export function StoragesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useBackupStoragesApi();
  const [state, setState] = useState<State>({
    loading: true,
    error: null,
    availableLoading: true,
    availableError: null,
  });
  const [userStorages, setUserStorages] = useState<SavedBackupModule[]>([]);
  const [availableStorages, setAvailableStorages] = useState<BackupStorage[]>(
    [],
  );

  useEffect(() => {
    let active = true;
    // load user-created storages
    api
      .list()
      .then((data) => {
        if (!active) return;
        setUserStorages(data);
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
      .listAvailable()
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
          <Card variant="outlined">
            <CardContent>
              <Typography color="text.secondary">
                {t("storages.noStorages")}
              </Typography>
            </CardContent>
          </Card>
        ) : (
          <Stack direction="row" spacing={2} flexWrap="wrap">
            {userStorages.map((s) => (
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
                  sx={{ position: "absolute", top: 4, right: 4 }}
                  onClick={async (e) => {
                    e.stopPropagation();
                    const result = await confirm({
                      title: t("storages.deleteTitle", {
                        defaultValue: "Delete storage",
                      }),
                      description: t("storages.deleteText", {
                        defaultValue: "This action is permanent!",
                      }),
                      confirmationText: t("common.delete", {
                        defaultValue: "Delete",
                      }),
                      cancellationText: t("common.cancel", {
                        defaultValue: "Cancel",
                      }),
                    });
                    if (result.confirmed) {
                      await api.delete(s.id);
                      setUserStorages((prev) =>
                        prev.filter((x) => x.id !== s.id),
                      );
                    }
                  }}
                >
                  <DeleteOutline fontSize="small" />
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
                    noWrap
                    sx={{
                      textAlign: "center",
                      maxWidth: 140,
                      fontSize: "0.7rem",
                      color: "text.secondary",
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
          {t("storages.addNew")}
        </Typography>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          {availableStorages.map((s) => (
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
        </Stack>
      </Box>
    </Stack>
  );
}

export default StoragesPage;
