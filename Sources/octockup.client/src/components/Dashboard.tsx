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
import { BackupStatus, BackupStatusType, User } from "../api/types";
import useAuthUser from "react-auth-kit/hooks/useAuthUser";
import { ProgressBarColor } from "./ProgressBar/ProgressBarColor";

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

  const getColorByStatus = (status: BackupStatusType): ProgressBarColor => {
    switch (status) {
      case BackupStatusType.Completed:
        return ProgressBarColor.Green;
      case BackupStatusType.Failed:
        return ProgressBarColor.Red;
      case BackupStatusType.Running:
        return ProgressBarColor.Yellow;
      case BackupStatusType.Created:
      default:
        return ProgressBarColor.Neutral;
    }
  };

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
                  <TableCell>{backup.lastRunDate.toLocaleString()}</TableCell>
                  <TableCell>{backup.duration}</TableCell>
                  <TableCell>
                    <ProgressBar
                      value={backup.progress}
                      color={getColorByStatus(backup.status)}
                    />
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
