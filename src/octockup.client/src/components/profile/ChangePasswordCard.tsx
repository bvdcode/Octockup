import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useAuthApi } from "../../api/authApi";
import { getApiErrorMessage } from "../../utils/apiError";

export default function ChangePasswordCard() {
  const { t } = useTranslation();
  const authApi = useAuthApi();
  const [oldPassword, setOldPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const canSave =
    oldPassword.trim().length > 0 &&
    newPassword.trim().length > 0 &&
    confirmation.trim().length > 0 &&
    !saving;

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedOldPassword = oldPassword.trim();
    const trimmedNewPassword = newPassword.trim();
    const trimmedConfirmation = confirmation.trim();

    setError(null);
    setSaved(false);
    if (trimmedNewPassword !== trimmedConfirmation) {
      setError(t("profile.password.mismatch"));
      return;
    }

    setSaving(true);
    try {
      await authApi.changePassword({
        oldPassword: trimmedOldPassword,
        newPassword: trimmedNewPassword,
      });
      setOldPassword("");
      setNewPassword("");
      setConfirmation("");
      setSaved(true);
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(
          getApiErrorMessage(caughtError, t("profile.password.failed")),
        );
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <CardContent>
        <Box component="form" onSubmit={handleSubmit}>
          <Stack spacing={2}>
            <Box>
              <Typography variant="h6">
                {t("profile.password.title")}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t("profile.password.description")}
              </Typography>
            </Box>
            {saved && (
              <Alert severity="success">{t("profile.password.saved")}</Alert>
            )}
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label={t("profile.password.current")}
              type="password"
              autoComplete="current-password"
              value={oldPassword}
              onChange={(event) => setOldPassword(event.target.value)}
              disabled={saving}
              fullWidth
            />
            <TextField
              label={t("profile.password.new")}
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              disabled={saving}
              fullWidth
            />
            <TextField
              label={t("profile.password.confirm")}
              type="password"
              autoComplete="new-password"
              value={confirmation}
              onChange={(event) => setConfirmation(event.target.value)}
              disabled={saving}
              fullWidth
            />
            <Box display="flex" justifyContent="flex-end">
              <Button type="submit" variant="contained" disabled={!canSave}>
                {saving
                  ? t("profile.password.saving")
                  : t("profile.password.save")}
              </Button>
            </Box>
          </Stack>
        </Box>
      </CardContent>
    </Card>
  );
}
