import {
  deleteJob,
  forceRunJob,
  getBackups,
  stopJob,
  triggerJob,
} from "../api/api";
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import useAuth from "../auth/useAuth";
import { toast } from "react-toastify";
import { API_BASE_URL } from "../config";
import { useEffect, useRef } from "react";
import { CustomDialog, ProgressBar } from ".";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ProgressBarColor } from "./ProgressBarColor";
import { BackupTaskStatus, User } from "../api/types";
import { Box, Typography, Button, IconButton } from "@mui/material";
import CustomDataGrid, { CustomDataGridRef } from "./CustomDataGrid";
import { Delete, Refresh, Replay, Stop, Visibility } from "@mui/icons-material";

const Dashboard: React.FC = () => {
  const { userState, accessToken } = useAuth();
  const user = userState as User;
  const { t } = useTranslation();
  const navigate = useNavigate();
  const gridRef = useRef<CustomDataGridRef>(null);
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
      gridRef.current?.reloadData();
    });
    hubConnection.current.start();
    return () => {
      if (hubConnection.current) {
        hubConnection.current.stop();
      }
    };
  }, [accessToken]);

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
      gridRef.current?.reloadData();
    });
  };

  return (
    <Box>
      <Box
        display="flex"
        justifyContent="space-between"
        alignItems="center"
        p={2}
      >
        <Box display="flex" flexDirection="row" gap={1}>
          <Typography variant="h4">{t("dashboard.title")}</Typography>
          <IconButton
            onClick={() => {
              gridRef.current?.reloadData();
              triggerJob();
            }}
            sx={{ minWidth: "unset" }}
          >
            <Refresh />
          </IconButton>
        </Box>
        <Typography
          variant="body1"
          sx={{ display: { xs: "none", sm: "block" } }}
        >
          {t("dashboard.updatedAt", { date: new Date().toLocaleString() })}
        </Typography>
        <Typography variant="body1" sx={{ gap: 1 }}>
          {t("dashboard.welcome", { name: user.username })}
        </Typography>
      </Box>
      <Box>
        <CustomDataGrid
          ref={gridRef}
          title={t("dashboard.tasks")}
          loadRows={async (page, pageSize, order) => {
            const orderByDesc =
              order?.[0]?.field === "id" && order?.[0]?.sort === "asc"
                ? false
                : true;
            const response = await getBackups(page, pageSize, orderByDesc);
            return { data: response.data, totalCount: response.totalCount };
          }}
          columns={[
            { field: "id", headerName: t("dashboard.id"), width: 50 },
            {
              field: "name",
              headerName: t("dashboard.jobName"),
              minWidth: 100,
            },
            {
              field: "completedAt",
              headerName: t("dashboard.completedAt"),
              width: 180,
              valueFormatter: (params) => new Date(params).toLocaleString(),
            },
            {
              field: "interval",
              headerName: t("dashboard.interval"),
              width: 100,
            },
            {
              field: "elapsed",
              headerName: t("dashboard.elapsed"),
              width: 100,
            },
            {
              field: "progress",
              headerName: t("dashboard.progress"),
              width: 200,
              renderCell: (params) => (
                <ProgressBar
                  value={params.value}
                  color={getColorByStatus(
                    params.row.status as BackupTaskStatus
                  )}
                />
              ),
            },
            {
              field: "status",
              headerName: t("dashboard.status"),
              width: 100,
              valueFormatter: (params) =>
                t("backupStatus." + BackupTaskStatus[params]),
            },
            { field: "lastMessage", headerName: t("dashboard.error"), flex: 1 },
            {
              field: "actions",
              headerName: t("dashboard.actions"),
              width: 150,
              renderCell: (params) => (
                <Box display="flex" justifyContent="space-around">
                  <Button
                    sx={{ minWidth: "unset" }}
                    onClick={() => {
                      navigate(`/backups/${params.row.id}`);
                    }}
                  >
                    <Visibility sx={{ cursor: "pointer" }} />
                  </Button>
                  {params.row.status !== BackupTaskStatus.Running ? (
                    <Button
                      sx={{ minWidth: "unset" }}
                      onClick={() =>
                        forceRunJob(params.row.id).then(() => {
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
                        stopJob(params.row.id).then(() => {
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
                      jobName: params.row.name,
                    })}
                    cancelText={t("cancel")}
                    confirmText={t("confirm")}
                    onConfirm={() => handleJobDelete(params.row.id)}
                  >
                    <Button sx={{ minWidth: "unset" }}>
                      <Delete sx={{ cursor: "pointer" }} />
                    </Button>
                  </CustomDialog>
                </Box>
              ),
            },
          ]}
          titleAlign="left"
          showToolbar={false}
        />
      </Box>
    </Box>
  );
};

export default Dashboard;
