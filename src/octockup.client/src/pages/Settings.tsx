import { Alert, Box, CircularProgress, Stack, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { useAuthApi } from "../api/authApi";
import AdminSettingsSection from "../components/settings/AdminSettingsSection";
import ConnectedAccountsCard from "../components/settings/ConnectedAccountsCard";
import DataTransferSettings from "../components/settings/DataTransferSettings";
import { queryKeys } from "../query/queryKeys";
import {
  clearOidcCallbackStatus,
  getOidcCallbackStatus,
} from "../utils/authUtils";

export default function SettingsPage() {
  const { t } = useTranslation();
  const authApi = useAuthApi();
  const queryClient = useQueryClient();
  const currentUserQuery = useQuery({
    queryKey: queryKeys.currentUser,
    queryFn: () => authApi.getCurrentUser(),
  });
  const currentUser = currentUserQuery.data;
  const loadFailed = currentUserQuery.isError && currentUser === undefined;
  const [oidcStatus, setOidcStatus] = useState(
    () => getOidcCallbackStatus(window.location.search),
  );

  useEffect(() => {
    if (oidcStatus === "linked" || oidcStatus === "error") {
      clearOidcCallbackStatus();
    }
  }, [oidcStatus]);

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
        <ConnectedAccountsCard />
        {currentUser === undefined && !loadFailed && (
          <Box display="flex" justifyContent="center" p={2}>
            <CircularProgress size={24} />
          </Box>
        )}
        <AdminSettingsSection
          isAdmin={currentUser?.isAdmin === true}
          onProvidersChanged={() =>
            void queryClient.invalidateQueries({
              queryKey: queryKeys.authenticationOptions,
            })
          }
        />
        <DataTransferSettings />
      </Stack>
    </Box>
  );
}
