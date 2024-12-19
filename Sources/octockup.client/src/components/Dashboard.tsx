import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  Box,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import { BackupStatus, User } from "../api/types";
import useAuthUser from "react-auth-kit/hooks/useAuthUser";
import { getBackupStatus } from "../api/api";
import { toast } from "react-toastify";
import { ProgressBar } from ".";

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
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>{t("backup.id")}</TableCell>
              <TableCell>{t("backup.jobName")}</TableCell>
              <TableCell>{t("backup.lastRun")}</TableCell>
              <TableCell>{t("backup.duration")}</TableCell>
              <TableCell>{t("backup.progress")}</TableCell>
              <TableCell>{t("backup.status")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {status.map((backup, index) => (
              <TableRow key={index}>
                <TableCell>{backup.id}</TableCell>
                <TableCell>{backup.jobName}</TableCell>
                <TableCell>{backup.lastRunDate.toLocaleString()}</TableCell>
                <TableCell>{backup.duration}</TableCell>
                <TableCell>
                  <ProgressBar
                    value={backup.progress}
                    error={backup.id === 52}
                  />
                </TableCell>
                <TableCell>{backup.status}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Box>
    </Box>
  );
};

export default Dashboard;
