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
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useConfirm } from "material-ui-confirm";
import { useAuthApi } from "../../api/authApi";
import { getApiErrorMessage } from "../../utils/apiError";

export default function AuthenticationSettingsCard() {
  const { t } = useTranslation();
  const confirm = useConfirm();
  const authApi = useAuthApi();
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    authApi
      .getAuthenticationSettings()
      .then((settings) => {
        if (active) {
          setEnabled(settings.passwordLoginEnabled);
        }
      })
      .catch((caughtError) => {
        if (active && caughtError instanceof Error) {
          setError(getApiErrorMessage(caughtError, t("settings.loadFailed")));
        }
      });
    return () => {
      active = false;
    };
  }, [authApi, t]);

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
    setError(null);
    try {
      const settings = await authApi.updateAuthenticationSettings({
        passwordLoginEnabled: nextEnabled,
      });
      setEnabled(settings.passwordLoginEnabled);
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(getApiErrorMessage(caughtError, t("settings.saveFailed")));
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
          {enabled === null ? (
            <CircularProgress size={24} />
          ) : (
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
          )}
          <Alert severity="info">
            {t("settings.authentication.disableHint")}
          </Alert>
        </Stack>
      </CardContent>
    </Card>
  );
}
