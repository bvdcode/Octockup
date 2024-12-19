import {
  Box,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
  Paper,
} from "@mui/material";
import { ProgressBar } from ".";
import { toast } from "react-toastify";
import styles from "./Dashboard.module.css";
import { useEffect, useState } from "react";
import { getBackupStatus } from "../api/api";
import { useTranslation } from "react-i18next";
import { BackupStatus, User } from "../api/types";
import useAuthUser from "react-auth-kit/hooks/useAuthUser";

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
    <Box className={styles.dashboardContainer}>
      <Box display="flex" justifyContent="space-between" alignItems="center">
        <Typography variant="h4">{t("dashboard.title")}</Typography>
        <Typography variant="body1">
          {t("dashboard.welcome", { name: authUser?.username })}
        </Typography>
      </Box>
      <Box mt={2} flexGrow={1} className={styles.tableContainer}>
        <TableContainer component={Paper}>
          <Table className={styles.table}>
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
                  <TableCell>{backup.lastRun}</TableCell>
                  <TableCell>{backup.duration}</TableCell>
                  <TableCell>
                    <ProgressBar value={backup.progress} />
                  </TableCell>
                  <TableCell>{backup.status}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>
    </Box>
  );
};

export default Dashboard;
