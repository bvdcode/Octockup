import {
  Box,
  Stack,
  Alert,
  Button,
  TextField,
  IconButton,
  Tooltip,
  Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { formatSize } from "../utils/formatUtils";
import { useAuthStore } from "@bvdcode/react-kit";
import type { SnapshotFileDto } from "../types/api";
import { useSnapshotsApi } from "../api/snapshotsApi";
import { ArrowBack, Download, Verified } from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { useQuery } from "@tanstack/react-query";
import { queryKeys } from "../query/queryKeys";
import { getApiErrorMessage } from "../utils/apiError";

export default function SnapshotFilesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { backupId, snapshotId } = useParams<{
    backupId: string;
    snapshotId: string;
  }>();
  const snapshotsApi = useSnapshotsApi();
  const accessToken = useAuthStore((s) => s.accessToken);
  const filesQuery = useQuery({
    queryKey: queryKeys.snapshotFiles(snapshotId ?? ""),
    queryFn: () => {
      if (!snapshotId) {
        throw new Error(t("snapshotFiles.missingSnapshotId"));
      }
      return snapshotsApi.getFiles(snapshotId);
    },
    enabled: Boolean(snapshotId),
  });
  const files = filesQuery.data ?? [];
  const [searchQuery, setSearchQuery] = useState("");
  const loadError = snapshotId
    ? filesQuery.error
    : new Error(t("snapshotFiles.missingSnapshotId"));

  const handleDownload = (fileId: string, validate: boolean) => {
    const url = new URL(
      `/api/v1/snapshots/${snapshotId}/files/${fileId}/download`,
      window.location.origin,
    );
    url.searchParams.set("access_token", accessToken || "");
    url.searchParams.set("validate", String(validate));
    window.open(url.toString(), "_blank");
  };

  const filteredFiles = files.filter((file) =>
    file.path.toLowerCase().includes(searchQuery.toLowerCase()),
  );

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
      width: 120,
      sortable: false,
      filterable: false,
      align: "center",
      headerAlign: "center",
      renderCell: (params) => (
        <Box display="flex" gap={0.5}>
          <Tooltip title={t("snapshotFiles.download")}>
            <span>
              <IconButton
                size="small"
                color="primary"
                disabled={!accessToken}
                onClick={() => handleDownload(params.row.id, false)}
              >
                <Download />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title={t("snapshotFiles.downloadValidated")}>
            <span>
              <IconButton
                size="small"
                color="primary"
                disabled={!accessToken}
                onClick={() => handleDownload(params.row.id, true)}
              >
                <Verified />
              </IconButton>
            </span>
          </Tooltip>
        </Box>
      ),
    },
  ];

  if (loadError && files.length === 0) {
    return (
      <Box p={2}>
        <Alert severity="error">
          {getApiErrorMessage(loadError, t("snapshotFiles.loadFailed"))}
        </Alert>
      </Box>
    );
  }

  return (
    <Stack spacing={3} display="flex" flexDirection="column" flex={1}>
      {loadError && (
        <Alert severity="error">
          {getApiErrorMessage(loadError, t("snapshotFiles.loadFailed"))}
        </Alert>
      )}
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
      <TextField
        fullWidth
        variant="outlined"
        placeholder={t("snapshotFiles.searchPlaceholder")}
        value={searchQuery}
        onChange={(e) => setSearchQuery(e.target.value)}
      />
      <Box flex={1}>
        <DataGrid
          rows={filteredFiles}
          columns={columns}
          loading={filesQuery.isPending && files.length === 0}
          pageSizeOptions={[10, 25, 50, 100]}
          autoPageSize
          initialState={{
            pagination: { paginationModel: { pageSize: 25 } },
            columns: {
              columnVisibilityModel: {
                name: false,
              },
            },
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
