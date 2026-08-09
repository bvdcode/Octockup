import { Add, Delete, Edit } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { useAuthApi } from "../../api/authApi";
import type {
  OidcProvider,
  SaveOidcProviderRequest,
} from "../../types/auth";
import { getApiErrorMessage } from "../../utils/apiError";
import OidcProviderDialog from "./OidcProviderDialog";
import { queryKeys } from "../../query/queryKeys";

interface OidcProvidersCardProps {
  onProvidersChanged: () => void;
}

export default function OidcProvidersCard({
  onProvidersChanged,
}: OidcProvidersCardProps) {
  const { t } = useTranslation();
  const authApi = useAuthApi();
  const queryClient = useQueryClient();
  const providersQuery = useQuery({
    queryKey: queryKeys.oidcProviders,
    queryFn: () => authApi.listOidcProviders(),
  });
  const providers = providersQuery.data ?? [];
  const [editing, setEditing] = useState<OidcProvider | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [deleting, setDeleting] = useState<OidcProvider | null>(null);
  const [saving, setSaving] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const error = providersQuery.error
    ? getApiErrorMessage(providersQuery.error, t("settings.loadFailed"))
    : null;

  const openCreate = () => {
    setEditing(null);
    setDialogError(null);
    setDialogOpen(true);
  };

  const openEdit = (provider: OidcProvider) => {
    setEditing(provider);
    setDialogError(null);
    setDialogOpen(true);
  };

  const handleSave = async (request: SaveOidcProviderRequest) => {
    setSaving(true);
    setDialogError(null);
    try {
      const savedProvider = editing
        ? await authApi.updateOidcProvider(editing.id, request)
        : await authApi.createOidcProvider(request);
      queryClient.setQueryData<OidcProvider[]>(
        queryKeys.oidcProviders,
        (current) => {
          const withoutSaved = (current ?? []).filter(
            (provider) => provider.id !== savedProvider.id,
          );
          return [...withoutSaved, savedProvider].sort((left, right) =>
            left.name.localeCompare(right.name),
          );
        },
      );
      setDialogOpen(false);
      onProvidersChanged();
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setDialogError(
          getApiErrorMessage(caughtError, t("settings.saveFailed")),
        );
      }
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!deleting) {
      return;
    }

    setSaving(true);
    setDeleteError(null);
    try {
      await authApi.deleteOidcProvider(deleting.id);
      queryClient.setQueryData<OidcProvider[]>(
        queryKeys.oidcProviders,
        (current) =>
          (current ?? []).filter((provider) => provider.id !== deleting.id),
      );
      setDeleting(null);
      setDeleteError(null);
      onProvidersChanged();
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setDeleteError(
          getApiErrorMessage(caughtError, t("settings.deleteFailed")),
        );
      }
    } finally {
      setSaving(false);
    }
  };

  const openDelete = (provider: OidcProvider) => {
    setDeleteError(null);
    setDeleting(provider);
  };

  const closeDelete = () => {
    setDeleteError(null);
    setDeleting(null);
  };

  return (
    <Card>
      <CardContent>
        <Stack spacing={2}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            justifyContent="space-between"
            alignItems={{ xs: "stretch", sm: "center" }}
            spacing={1}
          >
            <Box>
              <Typography variant="h6">{t("settings.oidc.title")}</Typography>
              <Typography variant="body2" color="text.secondary">
                {t("settings.oidc.description")}
              </Typography>
            </Box>
            <Button variant="contained" startIcon={<Add />} onClick={openCreate}>
              {t("settings.oidc.addProvider")}
            </Button>
          </Stack>
          {error && <Alert severity="error">{error}</Alert>}
          {providers.length === 0 ? (
            <Typography color="text.secondary">
              {t("settings.oidc.none")}
            </Typography>
          ) : (
            <Stack divider={<Divider flexItem />} spacing={1}>
              {providers.map((provider) => (
                <Stack
                  key={provider.id}
                  direction="row"
                  alignItems="center"
                  justifyContent="space-between"
                  spacing={1}
                >
                  <Box minWidth={0}>
                    <Stack direction="row" spacing={1} alignItems="center">
                      <Typography noWrap>{provider.name}</Typography>
                      <Chip
                        size="small"
                        color={provider.isEnabled ? "success" : "default"}
                        label={
                          provider.isEnabled
                            ? t("settings.oidc.enabled")
                            : t("settings.oidc.disabled")
                        }
                      />
                    </Stack>
                    <Typography variant="body2" color="text.secondary" noWrap>
                      {provider.issuer}
                    </Typography>
                  </Box>
                  <Stack direction="row">
                    <Tooltip title={t("common.edit")}>
                      <IconButton onClick={() => openEdit(provider)}>
                        <Edit />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={t("common.delete")}>
                      <IconButton
                        color="error"
                        onClick={() => openDelete(provider)}
                      >
                        <Delete />
                      </IconButton>
                    </Tooltip>
                  </Stack>
                </Stack>
              ))}
            </Stack>
          )}
        </Stack>
      </CardContent>

      {dialogOpen && (
        <OidcProviderDialog
          open
          provider={editing}
          saving={saving}
          error={dialogError}
          onClose={() => setDialogOpen(false)}
          onSave={handleSave}
        />
      )}

      <Dialog open={deleting !== null} onClose={closeDelete}>
        <DialogTitle>{t("settings.oidc.deleteTitle")}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography>
              {t("settings.oidc.deleteDescription", {
                provider: deleting?.name,
              })}
            </Typography>
            {deleteError && <Alert severity="error">{deleteError}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={closeDelete} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button
            color="error"
            variant="contained"
            onClick={handleDelete}
            disabled={saving}
          >
            {t("common.delete")}
          </Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
}
