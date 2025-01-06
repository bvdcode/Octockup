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
import useAuth from "../auth/useAuth";
import { toast } from "react-toastify";
import { API_BASE_URL } from "../config";
import styles from "./Dashboard.module.css";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { BackupTask, BackupTaskStatus, User } from "../api/types";
import { ProgressBarColor } from "./ProgressBar/ProgressBarColor";
import { deleteJob, forceRunJob, getBackupStatus, stopJob } from "../api/api";
import { Delete, Replay, Stop, Visibility } from "@mui/icons-material";
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";

const Dashboard: React.FC = () => {
  const { t } = useTranslation();
  const { userState, accessToken } = useAuth();
  const user = userState as User;
  const [jobs, setJobs] = useState<BackupTask[]>([]);
  const hubConnection = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!accessToken) {
      return;
    }
    hubConnection.current = new HubConnectionBuilder()
      .withUrl(API_BASE_URL + "/backup/hub?access_token=" + accessToken)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    hubConnection.current.on("Progress", () => {
      getBackupStatus().then((response) => {
        setJobs(response);
      });
    });
    hubConnection.current.start();
    return () => {
      if (hubConnection.current) {
        hubConnection.current.stop();
      }
    };
  }, [accessToken]);

  useEffect(() => {
    let isFetching = false;

    const loadData = async () => {
      if (isFetching) return;
      isFetching = true;
      try {
        const response = await getBackupStatus();
        setJobs(response);
      } finally {
        isFetching = false;
      }
    };

    loadData();

    const interval = setInterval(() => {
      loadData();
    }, 60000);

    return () => clearInterval(interval);
  }, []);

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
          {t("dashboard.updatedAt", { date: new Date().toLocaleString() })}
        </Typography>
        <Typography variant="body1">
          {t("dashboard.welcome", { name: user.username })}
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
                    <TableCell
                      sx={{ width: "50px", textAlign: "center" }}
                      component="th"
                      scope="row"
                    >
                      {backup.id}
                    </TableCell>
                    <TableCell
                      sx={{ width: "150px" }}
                      title={backup.name}
                      style={{ whiteSpace: "nowrap", overflow: "hidden" }}
                    >
                      {backup.name}
                    </TableCell>
                    <TableCell
                      sx={{ width: "200px" }}
                      title={backup.completedAt ?? "-"}
                    >
                      {backup.completedAtDate?.toLocaleString() ?? "-"}
                    </TableCell>
                    <TableCell
                      sx={{ width: "100px", textAlign: "center" }}
                      title={backup.interval}
                    >
                      {backup.interval}
                    </TableCell>
                    <TableCell
                      sx={{ width: "100px", textAlign: "center" }}
                      title={backup.elapsed}
                    >
                      {backup.elapsed}
                    </TableCell>
                    <TableCell
                      title={backup.progress * 100 + "%"}
                      sx={{ width: "200px" }}
                    >
                      <ProgressBar
                        value={backup.progress}
                        color={getColorByStatus(backup.status)}
                      />
                    </TableCell>
                    <TableCell
                      sx={{ width: "150px" }}
                      title={t(
                        "backupStatus." + BackupTaskStatus[backup.status]
                      )}
                    >
                      {t("backupStatus." + BackupTaskStatus[backup.status])}
                    </TableCell>
                    <TableCell>{backup.lastMessage ?? "-"}</TableCell>
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
                        <Button
                          onClick={() => {
                            deleteJob(backup.id).then(() => {
                              toast.success(t("dashboard.deleteSuccess"));
                              setJobs((prev) => {
                                return prev.filter(
                                  (job) => job.id !== backup.id
                                );
                              });
                            });
                          }}
                        >
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
