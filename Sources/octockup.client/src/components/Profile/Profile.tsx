import {
  Box,
  Button,
  Card,
  CardContent,
  TextField,
  Typography,
} from "@mui/material";
import { LanguageSwitcher } from "..";
import { useTranslation } from "react-i18next";
import AxiosClient from "../../api/AxiosClient";

const Profile: React.FC = () => {
  const { t } = useTranslation();
  return (
    <Box sx={{ p: 2 }}>
      <Typography variant="h1" component="h1">
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
          />
          <Button variant="contained" color="primary" fullWidth sx={{ mt: 2 }}>
            {t("profile.changePassword")}
          </Button>
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
    </Box>
  );
};

export default Profile;
