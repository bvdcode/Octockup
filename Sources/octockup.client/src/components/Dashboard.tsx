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
  Button,
} from "@mui/material";
import { ProgressBar } from ".";
import styles from "./Dashboard.module.css";
import { useEffect, useState } from "react";
import { forceRunJob, getBackupStatus, stopJob } from "../api/api";
import { useTranslation } from "react-i18next";
import { BackupTask, BackupTaskStatus, User } from "../api/types";
import useAuthUser from "react-auth-kit/hooks/useAuthUser";
import { ProgressBarColor } from "./ProgressBar/ProgressBarColor";
import { Delete, Replay, Stop, Visibility } from "@mui/icons-material";
import { toast } from "react-toastify";

const Dashboard: React.FC = () => {
  const { t } = useTranslation();
  const authUser = useAuthUser<User>();
  const [jobs, setJobs] = useState<BackupTask[]>([]);

  useEffect(() => {
    const loadData = async () => {
      getBackupStatus().then((response) => {
        setJobs(response);
      });
    };
    loadData();

    const interval = setInterval(() => {
      loadData();
    }, 1000);
    return () => clearInterval(interval);
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
                <TableCell>{t("dashboard.id")}</TableCell>
                <TableCell>{t("dashboard.jobName")}</TableCell>
                <TableCell>{t("dashboard.completedAt")}</TableCell>
                <TableCell>{t("dashboard.interval")}</TableCell>
                <TableCell>{t("dashboard.elapsed")}</TableCell>
                <TableCell>{t("dashboard.progress")}</TableCell>
                <TableCell>{t("dashboard.status")}</TableCell>
                <TableCell>{t("dashboard.error")}</TableCell>
                <TableCell>{t("dashboard.actions")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {jobs.length > 0 ? (
                jobs.map((backup, index) => (
                  <TableRow key={index}>
                    <TableCell>{backup.id}</TableCell>
                    <TableCell>{backup.name}</TableCell>
                    <TableCell>
                      {backup.completedAtDate?.toLocaleString() ?? "-"}
                    </TableCell>
                    <TableCell>{backup.interval}</TableCell>
                    <TableCell>{backup.elapsed}</TableCell>
                    <TableCell>
                      <ProgressBar
                        value={backup.progress}
                        color={getColorByStatus(backup.status)}
                      />
                    </TableCell>
                    <TableCell>
                      {t("backupStatus." + BackupTaskStatus[backup.status])}
                    </TableCell>
                    <TableCell>{backup.lastError ?? "-"}</TableCell>
                    <TableCell sx={{ width: "150px" }}>
                      <Box display="flex" justifyContent="space-around">
                        <Button>
                          <Visibility sx={{ cursor: "pointer" }} />
                        </Button>
                        {backup.status !== BackupTaskStatus.Running ? (
                          <Button
                            onClick={() =>
                              forceRunJob(backup.id).then(() => {
                                toast.success(t("dashboard.forceRunSuccess"));
                              })
                            }
                          >
                            <Replay sx={{ cursor: "pointer" }} />
                          </Button>
                        ) : (
                          <Button
                            onClick={() =>
                              stopJob(backup.id).then(() => {
                                toast.success(t("dashboard.stopSuccess"));
                              })
                            }
                          >
                            <Stop sx={{ cursor: "pointer" }} />
                          </Button>
                        )}
                        <Button>
                          <Delete sx={{ cursor: "pointer" }} />
                        </Button>
                      </Box>
                    </TableCell>
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
