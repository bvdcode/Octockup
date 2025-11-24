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
import type { BackupStorage } from "../types/api";
import { AddCircleOutline } from "@mui/icons-material";
import { getSourceIcon } from "../constants/sourceIcons";
import { useBackupStoragesApi } from "../api/backupStoragesApi";

interface State {
  loading: boolean;
  error: string | null;
}

export function StoragesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const api = useBackupStoragesApi();
  const [state, setState] = useState<State>({ loading: true, error: null });
  const [storages, setStorages] = useState<BackupStorage[]>([]);

  useEffect(() => {
    let active = true;
    api
      .list()
      .then((data) => {
        if (!active) return;
        setStorages(data);
        setState({ loading: false, error: null });
      })
      .catch((e) => {
        if (!active) return;
        setState({
          loading: false,
          error: e?.message || "Failed to load storages",
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
          {t("storages.title")}
        </Typography>
        <Card variant="outlined">
          <CardContent>
            <Typography color="text.secondary">
              {t("storages.noStorages")}
            </Typography>
          </CardContent>
        </Card>
      </Box>
      <Divider />
      <Box>
        <Typography variant="h6" gutterBottom>
          {t("storages.addNew")}
        </Typography>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          {storages.map((s) => (
            <Card
              key={s.id}
              sx={{
                minWidth: 120,
                cursor: "pointer",
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
                  gap: 1,
                }}
              >
                <Box sx={{ fontSize: 32 }}>{getSourceIcon(s.id)}</Box>
                <Typography variant="caption">{s.name}</Typography>
              </CardContent>
            </Card>
          ))}
          {storages.length === 0 && (
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
