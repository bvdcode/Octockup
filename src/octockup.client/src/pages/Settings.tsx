import { Alert, Box, CircularProgress, Stack, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuthApi } from "../api/authApi";
import AdminSettingsSection from "../components/settings/AdminSettingsSection";
import ConnectedAccountsCard from "../components/settings/ConnectedAccountsCard";
import DataTransferSettings from "../components/settings/DataTransferSettings";
import type { CurrentUser } from "../types/auth";
import {
  clearOidcCallbackStatus,
  getOidcCallbackStatus,
} from "../utils/authUtils";

export default function SettingsPage() {
  const { t } = useTranslation();
  const authApi = useAuthApi();
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [oidcRevision, setOidcRevision] = useState(0);
  const [oidcStatus, setOidcStatus] = useState(
    () => getOidcCallbackStatus(window.location.search),
  );

  useEffect(() => {
    if (oidcStatus === "linked" || oidcStatus === "error") {
      clearOidcCallbackStatus();
    }
  }, [oidcStatus]);

  useEffect(() => {
    let active = true;

    authApi
      .getCurrentUser()
      .then((user) => {
        if (active) {
          setCurrentUser(user);
        }
      })
      .catch(() => {
        if (active) {
          setLoadFailed(true);
        }
      });

    return () => {
      active = false;
    };
  }, [authApi]);

  return (
    <Box p={2}>
      <Typography variant="h4" gutterBottom>
        {t("settings.title")}
      </Typography>
      <Stack spacing={2}>
        {oidcStatus === "linked" && (
          <Alert severity="success" onClose={() => setOidcStatus(null)}>
            {t("settings.connectedAccounts.linked")}
          </Alert>
        )}
        {oidcStatus === "error" && (
          <Alert severity="error" onClose={() => setOidcStatus(null)}>
            {t("settings.linkFailed")}
          </Alert>
        )}
        {loadFailed && (
          <Alert severity="error">{t("settings.loadFailed")}</Alert>
        )}
        <ConnectedAccountsCard key={oidcRevision} />
        {currentUser === null && !loadFailed && (
          <Box display="flex" justifyContent="center" p={2}>
            <CircularProgress size={24} />
          </Box>
        )}
        <AdminSettingsSection
          isAdmin={currentUser?.isAdmin === true}
          onProvidersChanged={() =>
            setOidcRevision((currentRevision) => currentRevision + 1)
          }
        />
        <DataTransferSettings />
      </Stack>
    </Box>
  );
}
