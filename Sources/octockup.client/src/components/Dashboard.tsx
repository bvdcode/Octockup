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
import { BackupTask, BackupTaskStatus, User } from "../api/types";
import useAuthUser from "react-auth-kit/hooks/useAuthUser";
import { ProgressBarColor } from "./ProgressBar/ProgressBarColor";

const Dashboard: React.FC = () => {
  const { t } = useTranslation();
  const authUser = useAuthUser<User>();
  const [jobs, setJobs] = useState<BackupTask[]>([]);

  useEffect(() => {
    getBackupStatus()
      .then((response) => {
        setJobs(response);
      })
      .catch((error) => {
        toast.error(t("dataLoadError", { error: error.message }));
      });
  }, [t]);

  const getColorByStatus = (status: BackupTaskStatus): ProgressBarColor => {
    switch (status) {
      case BackupTaskStatus.Completed:
        return ProgressBarColor.Green;
      case BackupTaskStatus.Failed:
        return ProgressBarColor.Red;
      case BackupTaskStatus.Running:
        return ProgressBarColor.Yellow;
      case BackupTaskStatus.Created:
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
              {jobs.length > 0 ? (
                jobs.map((backup, index) => (
                  <TableRow key={index}>
                    <TableCell>{backup.id}</TableCell>
                    <TableCell>{backup.name}</TableCell>
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
                ))
              ) : (
                <TableRow>
                  <TableCell sx={{ textAlign: "center" }} colSpan={6}>
                    {t("backup.noData")}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>
    </Box>
  );
};

export default Dashboard;
