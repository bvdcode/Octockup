import { Link, LinkOff } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { useAuthApi } from "../../api/authApi";
import type {
  ExternalIdentity,
  PublicOidcProvider,
} from "../../types/auth";
import { getApiErrorMessage } from "../../utils/apiError";
import { getUnlinkedProviders } from "../../utils/authUtils";
import { queryKeys } from "../../query/queryKeys";

export default function ConnectedAccountsCard() {
  const { t } = useTranslation();
  const authApi = useAuthApi();
  const queryClient = useQueryClient();
  const identitiesQuery = useQuery({
    queryKey: queryKeys.externalIdentities,
    queryFn: () => authApi.listExternalIdentities(),
  });
  const optionsQuery = useQuery({
    queryKey: queryKeys.authenticationOptions,
    queryFn: () => authApi.getOptions(),
  });
  const identities = useMemo<ExternalIdentity[]>(
    () => identitiesQuery.data ?? [],
    [identitiesQuery.data],
  );
  const providers = useMemo<PublicOidcProvider[]>(
    () => optionsQuery.data?.oidcProviders ?? [],
    [optionsQuery.data],
  );
  const loading =
    (identitiesQuery.isPending && identitiesQuery.data === undefined) ||
    (optionsQuery.isPending && optionsQuery.data === undefined);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const queryError = identitiesQuery.error ?? optionsQuery.error;
  const error = actionError ??
    (queryError
      ? getApiErrorMessage(queryError, t("settings.loadFailed"))
      : null);

  const availableProviders = useMemo(
    () => getUnlinkedProviders(providers, identities),
    [identities, providers],
  );

  const handleLink = async (provider: PublicOidcProvider) => {
    setBusyId(provider.slug);
    setActionError(null);
    try {
      const authorizationUrl = await authApi.beginOidcAuthorization(
        provider.slug,
        "/settings",
        true,
      );
      window.location.assign(authorizationUrl);
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setActionError(
          getApiErrorMessage(caughtError, t("settings.linkFailed")),
        );
      }
      setBusyId(null);
    }
  };

  const handleUnlink = async (identityId: string) => {
    setBusyId(identityId);
    setActionError(null);
    try {
      await authApi.unlinkExternalIdentity(identityId);
      queryClient.setQueryData<ExternalIdentity[]>(
        queryKeys.externalIdentities,
        (current) =>
          (current ?? []).filter((identity) => identity.id !== identityId),
      );
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setActionError(
          getApiErrorMessage(caughtError, t("settings.unlinkFailed")),
        );
      }
    } finally {
      setBusyId(null);
    }
  };

  return (
    <Card>
      <CardContent>
        <Stack spacing={2}>
          <Box>
            <Typography variant="h6">
              {t("settings.connectedAccounts.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t("settings.connectedAccounts.description")}
            </Typography>
          </Box>
          {error && <Alert severity="error">{error}</Alert>}
          {loading ? (
            <Box display="flex" justifyContent="center">
              <CircularProgress size={24} />
            </Box>
          ) : (
            <Stack spacing={1.5} divider={<Divider flexItem />}>
              {identities.length === 0 && (
                <Typography color="text.secondary">
                  {t("settings.connectedAccounts.none")}
                </Typography>
              )}
              {identities.map((identity) => (
                <Stack
                  key={identity.id}
                  direction={{ xs: "column", sm: "row" }}
                  justifyContent="space-between"
                  alignItems={{ xs: "stretch", sm: "center" }}
                  spacing={1}
                >
                  <Box>
                    <Typography>{identity.providerName}</Typography>
                    {(identity.email || identity.displayName) && (
                      <Typography variant="body2" color="text.secondary">
                        {identity.email ?? identity.displayName}
                      </Typography>
                    )}
                  </Box>
                  <Button
                    color="error"
                    variant="outlined"
                    startIcon={<LinkOff />}
                    disabled={busyId !== null}
                    onClick={() => handleUnlink(identity.id)}
                  >
                    {t("settings.connectedAccounts.unlink")}
                  </Button>
                </Stack>
              ))}
              {availableProviders.map((provider) => (
                <Button
                  key={provider.slug}
                  variant="outlined"
                  startIcon={<Link />}
                  disabled={busyId !== null}
                  onClick={() => handleLink(provider)}
                >
                  {t("settings.connectedAccounts.link", {
                    provider: provider.name,
                  })}
                </Button>
              ))}
            </Stack>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}
