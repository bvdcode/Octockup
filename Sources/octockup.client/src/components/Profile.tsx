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
import useAuth from "../auth/useAuth";
import { toast } from "react-toastify";
import { changePassword } from "../api/api";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { MIN_PASSWORD_LENGTH } from "../config";
import { useAppTheme } from "../hooks/useAppTheme";

const Profile: React.FC = () => {
  const { signOut } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { isDarkMode, toggleTheme } = useAppTheme();
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

  const handleLogout = () => {
    signOut();
    navigate("/login");
  };

  return (
    <Box sx={{ p: 2, maxWidth: 400, margin: "auto" }}>
      <Typography variant="h4" component="h3" sx={{ textAlign: "center" }}>
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
          <Box
            display="flex"
            alignItems="center"
            justifyContent="space-between"
          >
            <Typography variant="h6" component="h6">
              {t("profile.theme")}
            </Typography>
            <Button variant="text" onClick={toggleTheme}>
              {isDarkMode ? "🌙" : "☀️"}
            </Button>
          </Box>
        </CardContent>
      </Card>
      <Card sx={{ my: 2, p: 2 }}>
        <CardContent>
          <Button
            variant="contained"
            color="secondary"
            fullWidth
            onClick={handleLogout}
          >
            {t("profile.logout")}
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
};

export default Profile;
