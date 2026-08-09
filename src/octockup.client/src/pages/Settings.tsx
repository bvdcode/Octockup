import { Alert, Box, CircularProgress, Stack, Typography } from "@mui/material";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { useAuthApi } from "../api/authApi";
import AdminSettingsSection from "../components/settings/AdminSettingsSection";
import DataTransferSettings from "../components/settings/DataTransferSettings";
import { queryKeys } from "../query/queryKeys";

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
  return (
    <Box p={2}>
      <Typography variant="h4" gutterBottom>
        {t("settings.title")}
      </Typography>
      <Stack spacing={2}>
        {loadFailed && (
          <Alert severity="error">{t("settings.loadFailed")}</Alert>
        )}
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
