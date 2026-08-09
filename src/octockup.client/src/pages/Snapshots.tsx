import {
  Box,
  Card,
  Stack,
  Alert,
  Button,
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
import { ArrowBack } from "@mui/icons-material";
import { formatSize } from "../utils/formatUtils";
import { useNavigate, useParams } from "react-router-dom";
import { useSnapshotsApi } from "../api/snapshotsApi";
import type { SnapshotDto } from "../types/api";
import { useAuthStore } from "@bvdcode/react-kit";
import { confirm } from "material-ui-confirm";
import SnapshotActionsCell from "../components/snapshots/SnapshotActionsCell";
import { getApiErrorMessage } from "../utils/apiError";

interface State {
  loading: boolean;
  error: string | null;
}

export default function SnapshotsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { backupId } = useParams<{ backupId: string }>();
  const snapshotsApi = useSnapshotsApi();
  const accessToken = useAuthStore((s) => s.accessToken);
  const [state, setState] = useState<State>({
    loading: true,
    error: backupId ? null : t("snapshots.missingBackupId"),
  });
  const [snapshots, setSnapshots] = useState<SnapshotDto[]>([]);
  const [copyMessage, setCopyMessage] = useState<string | null>(null);
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
      .catch((caughtError: Error) => {
        if (!active) return;
        setState({
          loading: false,
          error: getApiErrorMessage(caughtError, t("snapshots.loadFailed")),
        });
      });
    return () => {
      active = false;
    };
  }, [backupId, snapshotsApi, t]);

  const createSnapshotDownloadUrl = (
    snapshotId: string,
    validate: boolean,
  ) => {
    const url = new URL(
      `/api/v1/snapshots/${snapshotId}/download`,
      window.location.origin,
    );
    url.searchParams.set("access_token", accessToken || "");
    url.searchParams.set("validate", String(validate));
    return url.toString();
  };

  const handleDownload = (snapshotId: string, validate: boolean) => {
    window.open(createSnapshotDownloadUrl(snapshotId, validate), "_blank");
  };

  const handleCopyDownloadLink = async (
    snapshotId: string,
    validate: boolean,
  ) => {
    await navigator.clipboard.writeText(
      createSnapshotDownloadUrl(snapshotId, validate),
    );
    setCopyMessage(
      t(validate ? "snapshots.validatedLinkCopied" : "snapshots.linkCopied"),
    );
  };

  const handleDelete = async (snapshot: SnapshotDto) => {
    const result = await confirm({
      title: t("snapshots.deleteTitle"),
      description: t("snapshots.deleteText"),
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
      await snapshotsApi.deleteSnapshot(snapshot.id);
      setSnapshots((current) =>
        current.filter((item) => item.id !== snapshot.id),
      );
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setState((current) => ({
          ...current,
          error: getApiErrorMessage(caughtError, t("snapshots.deleteFailed")),
        }));
      }
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
      width: 112,
      sortable: false,
      filterable: false,
      align: "center",
      headerAlign: "center",
      cellClassName: "snapshot-actions-cell",
      renderCell: (params) => (
        <SnapshotActionsCell
          downloadDisabled={!accessToken || !params.row.completedAt}
          deleting={deletingId === params.row.id}
          onDownload={(validate) => handleDownload(params.row.id, validate)}
          onCopyLink={(validate) =>
            handleCopyDownloadLink(params.row.id, validate)
          }
          onDelete={() => handleDelete(params.row)}
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
      {copyMessage && (
        <Alert severity="success" onClose={() => setCopyMessage(null)}>
          {copyMessage}
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
              "& .snapshot-actions-cell": {
                overflow: "visible",
              },
            }}
          />
        </Box>
      )}
    </Stack>
  );
}
