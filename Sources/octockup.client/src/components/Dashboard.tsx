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
  Pagination,
  TextField,
} from "@mui/material";
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import { CustomDialog, ProgressBar } from ".";
import useAuth from "../auth/useAuth";
import { toast } from "react-toastify";
import { API_BASE_URL } from "../config";
import styles from "./Dashboard.module.css";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useEffect, useRef, useState } from "react";
import { BackupTask, BackupTaskStatus, User } from "../api/types";
import { ProgressBarColor } from "./ProgressBar/ProgressBarColor";
import { Delete, Refresh, Replay, Stop, Visibility } from "@mui/icons-material";
import {
  deleteJob,
  forceRunJob,
  getBackups,
  stopJob,
  triggerJob,
} from "../api/api";

const Dashboard: React.FC = () => {
  const { t } = useTranslation();
  const { userState, accessToken } = useAuth();
  const user = userState as User;
  const [jobs, setJobs] = useState<BackupTask[]>([]);
  const hubConnection = useRef<HubConnection | null>(null);
  const navigate = useNavigate();
  const [totalCount, setTotalCount] = useState<number>(0);
  const [page, setPage] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);

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
      getBackups().then((response) => {
        setJobs(response.data);
        setTotalCount(response.totalCount);
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
      if (isFetching) {
        return;
      }
      isFetching = true;
      try {
        const response = await getBackups(page, pageSize);
        setJobs(response.data);
        setTotalCount(response.totalCount);
      } finally {
        isFetching = false;
      }
    };

    loadData();

    const interval = setInterval(() => {
      loadData();
    }, 60000);

    return () => clearInterval(interval);
  }, [page, pageSize]);

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

  const handleJobDelete = async (backupId: number) => {
    deleteJob(backupId).then(() => {
      toast.success(t("dashboard.deleteSuccess"));
      setJobs((prev) => {
        return prev.filter((job) => job.id !== backupId);
      });
    });
  };

  return (
    <Box className={styles.dashboardContainer}>
      <Box display="flex" justifyContent="space-between" alignItems="center">
        <Typography variant="h4">{t("dashboard.title")}</Typography>
        <Typography
          variant="body1"
          sx={{ display: { xs: "none", sm: "block" } }}
        >
          {t("dashboard.updatedAt", { date: new Date().toLocaleString() })}
        </Typography>
        <Typography variant="body1" sx={{ gap: 1 }}>
          {t("dashboard.welcome", { name: user.username })}
          <Button onClick={triggerJob} sx={{ minWidth: "unset" }}>
            <Refresh />
          </Button>
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
                      sx={{ width: "180px" }}
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
                      sx={{ width: "100px" }}
                      title={t(
                        "backupStatus." + BackupTaskStatus[backup.status]
                      )}
                    >
                      {t("backupStatus." + BackupTaskStatus[backup.status])}
                    </TableCell>
                    <TableCell>{backup.lastMessage ?? "-"}</TableCell>
                    <TableCell sx={{ width: "150px" }}>
                      <Box display="flex" justifyContent="space-around">
                        <Button
                          sx={{ minWidth: "unset" }}
                          onClick={() => {
                            navigate(`/backups/${backup.id}`);
                          }}
                        >
                          <Visibility sx={{ cursor: "pointer" }} />
                        </Button>
                        {backup.status !== BackupTaskStatus.Running ? (
                          <Button
                            sx={{ minWidth: "unset" }}
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
                            sx={{ minWidth: "unset" }}
                            onClick={() =>
                              stopJob(backup.id).then(() => {
                                toast.success(t("dashboard.stopSuccess"));
                              })
                            }
                          >
                            <Stop sx={{ cursor: "pointer" }} />
                          </Button>
                        )}

                        <CustomDialog
                          title={t("dashboard.deleteTitle")}
                          content={t("dashboard.deleteMessage", {
                            jobName: backup.name,
                          })}
                          cancelText={t("cancel")}
                          confirmText={t("confirm")}
                          onConfirm={() => handleJobDelete(backup.id)}
                        >
                          <Button sx={{ minWidth: "unset" }}>
                            <Delete sx={{ cursor: "pointer" }} />
                          </Button>
                        </CustomDialog>
                      </Box>
                    </TableCell>
                  </TableRow>
                ))
              ) : (
                <TableRow>
                  <TableCell sx={{ textAlign: "center" }} colSpan={9}>
                    {t("dashboard.noJobs")}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
        <Box
          display="flex"
          justifyContent="center"
          alignItems="center"
          mt={2}
          sx={{ marginBottom: { xs: 5 } }}
        >
          <Pagination
            count={Math.ceil(totalCount / pageSize)}
            color="primary"
            shape="rounded"
            size="medium"
            showFirstButton
            showLastButton
            boundaryCount={0}
            page={page}
            onChange={(_, page) => {
              setPage(page);
            }}
          />
          <Box ml={2} sx={{ display: { xs: "none", sm: "block" } }}>
            <TextField
              select
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
              variant="outlined"
              size="small"
              sx={{ position: "absolute", right: 15, marginTop: "-20px" }}
              slotProps={{
                select: {
                  native: true,
                },
              }}
            >
              {[1, 5, 10, 25, 50, 100].map((size) => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </TextField>
          </Box>
        </Box>
      </Box>
    </Box>
  );
};

export default Dashboard;
