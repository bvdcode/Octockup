import {
  Alert,
  Box,
  Card,
  CardContent,
  CircularProgress,
  FormControlLabel,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useAuthApi } from "../../api/authApi";
import { getApiErrorMessage } from "../../utils/apiError";
import { queryKeys } from "../../query/queryKeys";
import type { AuthenticationSettings } from "../../types/auth";

export default function AuthenticationSettingsCard() {
  const { t } = useTranslation();
  const confirm = useConfirm();
  const authApi = useAuthApi();
  const queryClient = useQueryClient();
  const settingsQuery = useQuery({
    queryKey: queryKeys.authenticationSettings,
    queryFn: () => authApi.getAuthenticationSettings(),
  });
  const enabled = settingsQuery.data?.passwordLoginEnabled ?? null;
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const error = actionError ??
    (settingsQuery.error
      ? getApiErrorMessage(settingsQuery.error, t("settings.loadFailed"))
      : null);

  const handleChange = async (nextEnabled: boolean) => {
    if (!nextEnabled) {
      const result = await confirm({
        title: t("settings.authentication.disableTitle"),
        description: t("settings.authentication.disableDescription"),
        confirmationText: t("settings.authentication.disableConfirm"),
        cancellationText: t("common.cancel"),
        confirmationButtonProps: { color: "error", variant: "contained" },
      });
      if (!result.confirmed) {
        return;
      }
    }

    setSaving(true);
    setActionError(null);
    try {
      const settings = await authApi.updateAuthenticationSettings({
        passwordLoginEnabled: nextEnabled,
      });
      queryClient.setQueryData<AuthenticationSettings>(
        queryKeys.authenticationSettings,
        settings,
      );
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setActionError(
          getApiErrorMessage(caughtError, t("settings.saveFailed")),
        );
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <CardContent>
        <Stack spacing={2}>
          <Box>
            <Typography variant="h6">
              {t("settings.authentication.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t("settings.authentication.description")}
            </Typography>
          </Box>
          {error && <Alert severity="error">{error}</Alert>}
          {enabled === null && settingsQuery.isPending ? (
            <CircularProgress size={24} />
          ) : enabled !== null ? (
            <FormControlLabel
              control={
                <Switch
                  checked={enabled}
                  disabled={saving}
                  onChange={(event) => handleChange(event.target.checked)}
                />
              }
              label={t("settings.authentication.passwordLogin")}
            />
          ) : null}
          <Alert severity="info">
            {t("settings.authentication.disableHint")}
          </Alert>
        </Stack>
      </CardContent>
    </Card>
  );
}
