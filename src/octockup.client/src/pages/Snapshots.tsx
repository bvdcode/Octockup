import {
  Box,
  Card,
  Stack,
  Alert,
  Button,
  Typography,
  CardContent,
  CircularProgress,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import {
  DataGrid,
  type GridColDef,
  type GridRowParams,
} from "@mui/x-data-grid";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ArrowBack } from "@mui/icons-material";
import { formatSize } from "../utils/formatUtils";
import { useNavigate, useParams } from "react-router-dom";
import { useSnapshotsApi } from "../api/snapshotsApi";
import type { SnapshotDto } from "../types/api";
import { confirm } from "material-ui-confirm";
import { isAxiosError } from "axios";
import SnapshotActions from "../components/SnapshotActions";
import SnapshotArchiveProgress from "../components/SnapshotArchiveProgress";
import { useSnapshotArchiveJobs } from "../hooks/useSnapshotArchiveJobs";
import { useSnapshotArchiveActions } from "../hooks/useSnapshotArchiveActions";
import SnapshotMobileList from "../components/SnapshotMobileList";

interface State {
  loading: boolean;
  error: string | null;
}

interface ApiErrorResponse {
  message?: string;
}

export default function SnapshotsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const { backupId } = useParams<{ backupId: string }>();
  const snapshotsApi = useSnapshotsApi();
  const archiveJobs = useSnapshotArchiveJobs(backupId);
  const archiveActions = useSnapshotArchiveActions(
    archiveJobs.upsertJob,
    archiveJobs.reload,
  );
  const [state, setState] = useState<State>({
    loading: true,
    error: backupId ? null : t("snapshots.missingBackupId"),
  });
  const [snapshots, setSnapshots] = useState<SnapshotDto[]>([]);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  useEffect(() => {
    if (!backupId) return;

    let active = true;
    snapshotsApi
      .listByBackup(backupId)
      .then((snapshotList) => {
        if (!active) return;
        setSnapshots(snapshotList);
        setState({ loading: false, error: null });
      })
      .catch((error) => {
        if (!active) return;
        setState({
          loading: false,
          error:
            error instanceof Error
              ? error.message
              : t("snapshots.loadFailed"),
        });
      });
    return () => {
      active = false;
    };
  }, [backupId, snapshotsApi, t]);

  const handleDelete = async (snapshot: SnapshotDto) => {
    const result = await confirm({
      title: t("snapshots.deleteTitle"),
      description: t("snapshots.deleteText", {
        count: snapshot.filesCount,
        size: formatSize(snapshot.totalSize),
      }),
      confirmationText: t("common.delete"),
      cancellationText: t("common.cancel"),
      confirmationButtonProps: { color: "error" },
    });

    if (!result.confirmed) {
      return;
    }

    setDeletingId(snapshot.id);
    setState((current) => ({ ...current, error: null }));
    try {
      const deleteResult = await snapshotsApi.delete(snapshot.id);
      setSnapshots((current) =>
        current.filter((item) => item.id !== snapshot.id),
      );
      setSuccessMessage(
        t("snapshots.deleteSuccess", {
          count: deleteResult.deletedSnapshotFiles,
          size: formatSize(deleteResult.deletedSnapshotFileBytes),
        }),
      );
    } catch (error) {
      const message = isAxiosError<ApiErrorResponse>(error)
        ? error.response?.data?.message || t("snapshots.deleteFailed")
        : t("snapshots.deleteFailed");
      setState((current) => ({ ...current, error: message }));
    } finally {
      setDeletingId(null);
    }
  };

  const columns: GridColDef<SnapshotDto>[] = [
    {
      field: "completedAt",
      headerName: t("snapshots.completedAt"),
      flex: 1,
      minWidth: 200,
      valueGetter: (value: string | null) =>
        value ? new Date(value).getTime() : 0,
      renderCell: (params) =>
        params.row.completedAt
          ? new Date(params.row.completedAt).toLocaleString()
          : t("snapshots.never"),
    },
    {
      field: "filesCount",
      headerName: t("snapshots.filesCount"),
      flex: 1,
      minWidth: 150,
      align: "right",
      headerAlign: "right",
      valueFormatter: (value: number) => value.toLocaleString(),
    },
    {
      field: "totalSize",
      headerName: t("snapshots.totalSize"),
      flex: 1,
      minWidth: 150,
      align: "right",
      headerAlign: "right",
      valueFormatter: (value: number) => formatSize(value),
    },
    {
      field: "archive",
      headerName: t("snapshots.archive.title"),
      width: 360,
      sortable: false,
      filterable: false,
      renderCell: (params) => (
        <SnapshotArchiveProgress
          job={archiveJobs.jobsBySnapshot[params.row.id]}
        />
      ),
    },
    {
      field: "actions",
      headerName: t("snapshots.actions"),
      width: 196,
      sortable: false,
      filterable: false,
      align: "center",
      headerAlign: "center",
      renderCell: (params) => (
        <SnapshotActions
          snapshot={params.row}
          archiveJob={archiveJobs.jobsBySnapshot[params.row.id]}
          deleting={deletingId === params.row.id}
          downloading={archiveActions.downloadingId === params.row.id}
          copying={archiveActions.copyingId === params.row.id}
          canceling={
            archiveActions.cancelingJobId ===
            archiveJobs.jobsBySnapshot[params.row.id]?.jobId
          }
          onDelete={handleDelete}
          onDownload={archiveActions.download}
          onCopyLink={archiveActions.copyLink}
          onCancel={archiveActions.cancel}
        />
      ),
    },
  ];

  const handleRowClick = (params: GridRowParams) => {
    navigate(`/backups/${backupId}/snapshots/${params.row.id}/files`);
  };

  if (state.error && snapshots.length === 0) {
    return (
      <Box p={2}>
        <Alert severity="error">{state.error}</Alert>
      </Box>
    );
  }

  return (
    <Stack spacing={3} display="flex" flexDirection="column" flex={1}>
      {state.error && <Alert severity="error">{state.error}</Alert>}
      {archiveJobs.loadFailed && (
        <Alert severity="error">{t("snapshots.archive.loadFailed")}</Alert>
      )}
      {archiveActions.error && (
        <Alert severity="error">{archiveActions.error}</Alert>
      )}
      {successMessage && (
        <Alert severity="success" onClose={() => setSuccessMessage(null)}>
          {successMessage}
        </Alert>
      )}
      {archiveActions.success && (
        <Alert severity="success" onClose={archiveActions.clearSuccess}>
          {archiveActions.success}
        </Alert>
      )}
      <Box display="flex" alignItems="center" gap={2}>
        <Button
          variant="outlined"
          startIcon={<ArrowBack />}
          onClick={() => navigate("/backups")}
        >
          {t("common.back")}
        </Button>
        <Typography variant="h5">{t("snapshots.title")}</Typography>
      </Box>
      {state.loading && snapshots.length === 0 ? (
        <Box display="flex" justifyContent="center" p={4}>
          <CircularProgress />
        </Box>
      ) : snapshots.length === 0 ? (
        <Card>
          <CardContent>
            <Typography color="text.secondary">
              {t("snapshots.noSnapshots")}
            </Typography>
          </CardContent>
        </Card>
      ) : isMobile ? (
        <SnapshotMobileList
          snapshots={snapshots}
          jobsBySnapshot={archiveJobs.jobsBySnapshot}
          deletingId={deletingId}
          downloadingId={archiveActions.downloadingId}
          copyingId={archiveActions.copyingId}
          cancelingJobId={archiveActions.cancelingJobId}
          onOpen={(snapshotId) =>
            navigate(`/backups/${backupId}/snapshots/${snapshotId}/files`)
          }
          onDelete={handleDelete}
          onDownload={archiveActions.download}
          onCopyLink={archiveActions.copyLink}
          onCancel={archiveActions.cancel}
        />
      ) : (
        <Box flex={1}>
          <DataGrid
            rows={snapshots}
            columns={columns}
            rowHeight={92}
            pageSizeOptions={[10, 25, 50, 100]}
            autoPageSize
            pagination
            initialState={{
              sorting: {
                sortModel: [{ field: "completedAt", sort: "desc" }],
              },
            }}
            onRowClick={handleRowClick}
            disableRowSelectionOnClick
            sx={{
              cursor: "pointer",
              "& .MuiDataGrid-row:hover": {
                cursor: "pointer",
              },
            }}
          />
        </Box>
      )}
    </Stack>
  );
}
