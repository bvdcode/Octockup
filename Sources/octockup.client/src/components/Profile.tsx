import {
  Box,
  Button,
  Card,
  CardContent,
  TextField,
  Typography,
} from "@mui/material";
import { useState } from "react";
import { AxiosError } from "axios";
import { LanguageSwitcher } from ".";
import { toast } from "react-toastify";
import { changePassword } from "../api/api";
import AxiosClient from "../api/AxiosClient";
import { useTranslation } from "react-i18next";
import { MIN_PASSWORD_LENGTH } from "../config";
const Profile: React.FC = () => {
  const { t } = useTranslation();
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const handlePasswordChange = () => {
    if (newPassword.length < MIN_PASSWORD_LENGTH) {
      toast.warn(
        t("profile.passwordLength", { minPasswordLength: MIN_PASSWORD_LENGTH })
      );
      return;
    }
    if (newPassword !== confirmPassword) {
      toast.warn(t("profile.passwordMismatch"));
      return;
    }
    changePassword(newPassword)
      .then(() => {
        toast.success(t("profile.passwordChanged"));
        setNewPassword("");
        setConfirmPassword("");
      })
      .catch((error: AxiosError) => {
        toast.error(t("profile.passwordChangeError", { error: error.message }));
      });
  };

  return (
    <Box sx={{ p: 2, maxWidth: 400, margin: "auto" }}>
      <Typography variant="h3" component="h3" sx={{ textAlign: "center" }}>
        {t("profile.title")}
      </Typography>
      <Card sx={{ my: 2, p: 2 }}>
        <CardContent>
          <TextField
            label={t("profile.newPassword")}
            type="password"
            variant="outlined"
            color="primary"
            fullWidth
            sx={{ mb: 2 }}
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
          />
          <TextField
            label={t("profile.confirmPassword")}
            type="password"
            variant="outlined"
            color="primary"
            fullWidth
            sx={{ mb: 2 }}
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
          />
          <Button
            variant="contained"
            color="primary"
            fullWidth
            sx={{ mt: 2 }}
            onClick={handlePasswordChange}
          >
            {t("profile.changePassword")}
          </Button>
        </CardContent>
      </Card>
      <Card sx={{ my: 2, p: 2 }}>
        <CardContent>
          <Box
            display="flex"
            alignItems="center"
            justifyContent="space-between"
          >
            <Typography variant="h6" component="h6">
              {t("profile.language")}
            </Typography>
            <LanguageSwitcher />
          </Box>
        </CardContent>
      </Card>
      <Card sx={{ my: 2, p: 2 }}>
        <CardContent>
          <Button
            variant="contained"
            color="secondary"
            fullWidth
            onClick={() => AxiosClient.events.emit("logout")}
          >
            {t("profile.logout")}
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
};

export default Profile;
