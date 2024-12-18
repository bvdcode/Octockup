import { User } from "../../api/types";
import { useTranslation } from "react-i18next";
import { Box, Typography } from "@mui/material";
import useAuthUser from "react-auth-kit/hooks/useAuthUser";

const Dashboard: React.FC = () => {
  const { t } = useTranslation();
  const authUser = useAuthUser<User>();

  return (
    <>
      <Box display="flex" justifyContent="space-between" alignItems="center">
        <Typography variant="h4">{t("dashboard.title")}</Typography>
        <Typography variant="body1">
          {t("dashboard.welcome", { name: authUser?.username })}
        </Typography>
      </Box>
      <Box mt={2}>{/* Main content goes here */}</Box>
    </>
  );
};

export default Dashboard;
