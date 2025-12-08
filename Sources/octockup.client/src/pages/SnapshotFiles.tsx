import {
  Box,
  Stack,
  Alert,
  Button,
  IconButton,
  Typography,
  CircularProgress,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatSize } from "../utils/formatUtils";
import type { SnapshotFileDto } from "../types/api";
import { useSnapshotsApi } from "../api/snapshotsApi";
import { ArrowBack, Download } from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";

interface State {
  loading: boolean;
  error: string | null;
}

export default function SnapshotFilesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { backupId, snapshotId } = useParams<{
    backupId: string;
    snapshotId: string;
  }>();
  const snapshotsApi = useSnapshotsApi();
  const [state, setState] = useState<State>({
    loading: !snapshotId,
    error: snapshotId ? null : "Snapshot ID is missing",
  });
  const [files, setFiles] = useState<SnapshotFileDto[]>([]);

  useEffect(() => {
    if (!snapshotId) return;

    let active = true;
    snapshotsApi
      .getFiles(snapshotId)
      .then((fileList) => {
        if (!active) return;
        setFiles(fileList);
        setState({ loading: false, error: null });
      })
      .catch((e) => {
        if (!active) return;
        setState({
          loading: false,
          error: e?.message || "Failed to load snapshot files",
        });
      });
    return () => {
      active = false;
    };
  }, [snapshotId, snapshotsApi]);

  const handleDownload = (fileId: string) => {
    const authData = localStorage.getItem("auth");
    let accessToken = "";
    if (authData) {
      try {
        const parsed = JSON.parse(authData);
        accessToken = parsed.accessToken || "";
      } catch {
        // ignore
      }
    }
    const url = `/api/v1/snapshots/${snapshotId}/files/${fileId}/download?access_token=${accessToken}`;
    window.open(url, "_blank");
  };

  const columns: GridColDef<SnapshotFileDto>[] = [
    {
      field: "name",
      headerName: t("snapshotFiles.name"),
      flex: 1,
      minWidth: 200,
    },
    {
      field: "path",
      headerName: t("snapshotFiles.path"),
      flex: 2,
      minWidth: 300,
    },
    {
      field: "size",
      headerName: t("snapshotFiles.size"),
      width: 120,
      align: "right",
      headerAlign: "right",
      valueFormatter: (value) => formatSize(value),
    },
    {
      field: "lastModified",
      headerName: t("snapshotFiles.lastModified"),
      width: 180,
      valueFormatter: (value) =>
        value ? new Date(value).toLocaleString() : "—",
    },
    {
      field: "actions",
      headerName: t("snapshotFiles.download"),
      width: 80,
      sortable: false,
      filterable: false,
      renderCell: (params) => (
        <IconButton
          size="small"
          color="primary"
          onClick={() => handleDownload(params.row.id)}
          title={t("snapshotFiles.download")}
        >
          <Download />
        </IconButton>
      ),
    },
  ];

  if (state.loading && files.length === 0) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }

  if (state.error && files.length === 0) {
    return (
      <Box p={2}>
        <Alert severity="error">{state.error}</Alert>
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      {state.error && <Alert severity="error">{state.error}</Alert>}
      <Box display="flex" alignItems="center" gap={2}>
        <Button
          variant="outlined"
          startIcon={<ArrowBack />}
          onClick={() => navigate(`/backups/${backupId}/snapshots`)}
        >
          {t("common.back")}
        </Button>
        <Typography variant="h5">{t("snapshotFiles.title")}</Typography>
      </Box>
      <Box sx={{ height: 600, width: "100%" }}>
        <DataGrid
          rows={files}
          columns={columns}
          pageSizeOptions={[25, 50, 100]}
          initialState={{
            pagination: { paginationModel: { pageSize: 25 } },
          }}
          disableRowSelectionOnClick
          sx={{
            "& .MuiDataGrid-cell:focus": {
              outline: "none",
            },
            "& .MuiDataGrid-cell:focus-within": {
              outline: "none",
            },
          }}
        />
      </Box>
    </Stack>
  );
}
