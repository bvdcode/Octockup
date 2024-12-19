import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Box, Typography } from "@mui/material";
import { BackupStatus, User } from "../api/types";
import useAuthUser from "react-auth-kit/hooks/useAuthUser";
import { getBackupStatus } from "../api/api";
import { toast } from "react-toastify";

const Dashboard: React.FC = () => {
  const { t } = useTranslation();
  const authUser = useAuthUser<User>();
  const [status, setStatus] = useState<BackupStatus[]>([]);

  useEffect(() => {
    getBackupStatus()
      .then((response) => {
        setStatus(response);
      })
      .catch((error) => {
        toast.error(t("dataLoadError", { error: error.message }));
      });
  }, [t]);

  return (
    <Box
      display="flex"
      flexDirection="column"
      height="100%"
      width="100%"
      padding={2}
    >
      <Box display="flex" justifyContent="space-between" alignItems="center">
        <Typography variant="h4">{t("dashboard.title")}</Typography>
        <Typography variant="body1">
          {t("dashboard.welcome", { name: authUser?.username })}
        </Typography>
      </Box>
      <Box mt={2} flexGrow={1}>
        {status.map((backup, index) => (
          <Box key={index} display="flex" justifyContent="space-between">
            <Typography>{backup.id}</Typography>
            <Typography>{backup.jobName}</Typography>
            <Typography>{backup.lastRun}</Typography>
            <Typography>{backup.duration}</Typography>
            <Typography>{backup.status}</Typography>
          </Box>
        ))}
      </Box>
    </Box>
  );
};

export default Dashboard;
