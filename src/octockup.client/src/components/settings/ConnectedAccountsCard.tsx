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
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuthApi } from "../../api/authApi";
import type {
  ExternalIdentity,
  PublicOidcProvider,
} from "../../types/auth";
import { getApiErrorMessage } from "../../utils/apiError";
import { getUnlinkedProviders } from "../../utils/authUtils";

export default function ConnectedAccountsCard() {
  const { t } = useTranslation();
  const authApi = useAuthApi();
  const [identities, setIdentities] = useState<ExternalIdentity[]>([]);
  const [providers, setProviders] = useState<PublicOidcProvider[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    Promise.all([authApi.listExternalIdentities(), authApi.getOptions()])
      .then(([loadedIdentities, options]) => {
        if (active) {
          setIdentities(loadedIdentities);
          setProviders(options.oidcProviders);
          setLoading(false);
        }
      })
      .catch((caughtError) => {
        if (active && caughtError instanceof Error) {
          setError(getApiErrorMessage(caughtError, t("settings.loadFailed")));
          setLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [authApi, t]);

  const availableProviders = useMemo(
    () => getUnlinkedProviders(providers, identities),
    [identities, providers],
  );

  const handleLink = async (provider: PublicOidcProvider) => {
    setBusyId(provider.slug);
    setError(null);
    try {
      const authorizationUrl = await authApi.beginOidcAuthorization(
        provider.slug,
        "/settings",
        true,
      );
      window.location.assign(authorizationUrl);
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(getApiErrorMessage(caughtError, t("settings.linkFailed")));
      }
      setBusyId(null);
    }
  };

  const handleUnlink = async (identityId: string) => {
    setBusyId(identityId);
    setError(null);
    try {
      await authApi.unlinkExternalIdentity(identityId);
      setIdentities((current) =>
        current.filter((identity) => identity.id !== identityId),
      );
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(getApiErrorMessage(caughtError, t("settings.unlinkFailed")));
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
