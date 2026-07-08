import {
  Box,
  Card,
  Stack,
  Alert,
  Button,
  Tooltip,
  IconButton,
  Typography,
  CardContent,
  CircularProgress,
} from "@mui/material";
import {
  DataGrid,
  type GridColDef,
  type GridRowParams,
} from "@mui/x-data-grid";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ArrowBack,
  ContentCopy,
  DeleteOutline,
  Download,
} from "@mui/icons-material";
import { formatSize } from "../utils/formatUtils";
import { useNavigate, useParams } from "react-router-dom";
import { useSnapshotsApi } from "../api/snapshotsApi";
import type { SnapshotDto } from "../types/api";
import { useAuthStore } from "@bvdcode/react-kit";
import { confirm } from "material-ui-confirm";
import { isAxiosError } from "axios";

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
  const { backupId } = useParams<{ backupId: string }>();
  const snapshotsApi = useSnapshotsApi();
  const accessToken = useAuthStore((s) => s.accessToken);
  const [state, setState] = useState<State>({
    loading: true,
    error: backupId ? null : "Backup ID is missing",
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
      .catch((e) => {
        if (!active) return;
        setState({
          loading: false,
          error: e?.message || "Failed to load snapshots",
        });
      });
    return () => {
      active = false;
    };
  }, [backupId, snapshotsApi]);

  const createSnapshotDownloadUrl = (snapshotId: string) => {
    const path = `/api/v1/snapshots/${snapshotId}/download?access_token=${encodeURIComponent(accessToken || "")}`;
    return new URL(path, window.location.origin).toString();
  };

  const handleDownload = (snapshotId: string) => {
    window.open(createSnapshotDownloadUrl(snapshotId), "_blank");
  };

  const handleCopyDownloadLink = async (snapshotId: string) => {
    await navigator.clipboard.writeText(createSnapshotDownloadUrl(snapshotId));
    setSuccessMessage(t("snapshots.linkCopied"));
  };

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
      field: "actions",
      headerName: t("snapshots.actions"),
      width: 156,
      sortable: false,
      filterable: false,
      align: "center",
      headerAlign: "center",
      renderCell: (params) => (
        <Box display="flex" gap={0.5}>
          <Tooltip title={t("snapshots.download")}>
            <span>
              <IconButton
                size="small"
                color="primary"
                disabled={!accessToken || !params.row.completedAt}
                onClick={(event) => {
                  event.stopPropagation();
                  handleDownload(params.row.id);
                }}
              >
                <Download />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title={t("snapshots.copyLink")}>
            <span>
              <IconButton
                size="small"
                color="primary"
                disabled={!accessToken || !params.row.completedAt}
                onClick={(event) => {
                  event.stopPropagation();
                  void handleCopyDownloadLink(params.row.id);
                }}
              >
                <ContentCopy />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title={t("snapshots.delete")}>
            <span>
              <IconButton
                size="small"
                color="error"
                disabled={deletingId === params.row.id}
                onClick={(event) => {
                  event.stopPropagation();
                  void handleDelete(params.row);
                }}
              >
                {deletingId === params.row.id ? (
                  <CircularProgress size={20} />
                ) : (
                  <DeleteOutline />
                )}
              </IconButton>
            </span>
          </Tooltip>
        </Box>
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
      {successMessage && (
        <Alert severity="success" onClose={() => setSuccessMessage(null)}>
          {successMessage}
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
      ) : (
        <Box flex={1}>
          <DataGrid
            rows={snapshots}
            columns={columns}
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
