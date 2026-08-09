import { Alert, Box, Stack, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import ChangePasswordCard from "../components/profile/ChangePasswordCard";
import ConnectedAccountsCard from "../components/settings/ConnectedAccountsCard";
import {
  clearOidcCallbackStatus,
  getOidcCallbackStatus,
} from "../utils/authUtils";

export default function ProfilePage() {
  const { t } = useTranslation();
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
      <Stack spacing={2}>
        <Box>
          <Typography variant="h4">{t("profile.title")}</Typography>
          <Typography variant="body2" color="text.secondary">
            {t("profile.description")}
          </Typography>
        </Box>
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
        <ConnectedAccountsCard />
        <ChangePasswordCard />
      </Stack>
    </Box>
  );
}
