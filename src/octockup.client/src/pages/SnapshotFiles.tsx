import {
  Box,
  Stack,
  Alert,
  Button,
  CircularProgress,
  TextField,
  IconButton,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatSize } from "../utils/formatUtils";
import type { SnapshotFileDto } from "../types/api";
import { useSnapshotsApi } from "../api/snapshotsApi";
import { ArrowBack, Download } from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import {
  DataGrid,
  type GridColDef,
  type GridPaginationModel,
} from "@mui/x-data-grid";
import { openTicketDownload } from "../utils/downloadUtils";

const defaultPageSize = 50;
const searchDebounceMs = 300;

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
    loading: true,
    error: snapshotId ? null : t("snapshotFiles.missingSnapshotId"),
  });
  const [files, setFiles] = useState<SnapshotFileDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [searchInput, setSearchInput] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: defaultPageSize,
  });
  const [pageCursors, setPageCursors] = useState<Map<number, string | null>>(
    new Map([[0, null]]),
  );
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const pageCursor = pageCursors.get(paginationModel.page);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const nextSearch = searchInput.trim();
      if (nextSearch === searchQuery) return;

      setSearchQuery(nextSearch);
      setPaginationModel((current) => ({ ...current, page: 0 }));
      setPageCursors(new Map([[0, null]]));
    }, searchDebounceMs);

    return () => window.clearTimeout(timer);
  }, [searchInput, searchQuery]);

  useEffect(() => {
    if (!snapshotId || (paginationModel.page > 0 && pageCursor === undefined)) {
      return;
    }

    let active = true;
    setState((current) => ({ ...current, loading: true, error: null }));
    snapshotsApi
      .getFiles(snapshotId, {
        pageSize: paginationModel.pageSize,
        cursor: pageCursor ?? undefined,
        search: searchQuery || undefined,
      })
      .then((page) => {
        if (!active) return;
        setFiles(page.items);
        setTotalCount(page.totalCount);
        setHasNextPage(page.hasNextPage);
        setPageCursors((current) => {
          const next = new Map(current);
          for (const pageNumber of next.keys()) {
            if (pageNumber > paginationModel.page + 1) {
              next.delete(pageNumber);
            }
          }
          if (page.nextCursor) {
            next.set(paginationModel.page + 1, page.nextCursor);
          } else {
            next.delete(paginationModel.page + 1);
          }
          return next;
        });
        setState({ loading: false, error: null });
      })
      .catch((error) => {
        if (!active) return;
        setState({
          loading: false,
          error:
            error instanceof Error
              ? error.message
              : t("snapshotFiles.loadFailed"),
        });
      });
    return () => {
      active = false;
    };
  }, [
    pageCursor,
    paginationModel.page,
    paginationModel.pageSize,
    searchQuery,
    snapshotId,
    snapshotsApi,
    t,
  ]);

  const handlePaginationChange = (nextModel: GridPaginationModel) => {
    if (nextModel.pageSize !== paginationModel.pageSize) {
      setPaginationModel({ page: 0, pageSize: nextModel.pageSize });
      setPageCursors(new Map([[0, null]]));
      return;
    }

    if (nextModel.page === 0 || pageCursors.has(nextModel.page)) {
      setPaginationModel(nextModel);
    }
  };

  const handleDownload = async (fileId: string) => {
    if (!snapshotId) return;

    setDownloadingId(fileId);
    setState((current) => ({ ...current, error: null }));
    try {
      await openTicketDownload(
        `/api/v1/snapshots/${encodeURIComponent(snapshotId)}/files/${encodeURIComponent(fileId)}/download`,
        () => snapshotsApi.createFileDownloadTicket(snapshotId, fileId),
      );
    } catch {
      setState((current) => ({
        ...current,
        error: t("snapshotFiles.downloadFailed"),
      }));
    } finally {
      setDownloadingId(null);
    }
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
      align: "center",
      headerAlign: "center",
      renderCell: (params) => (
        <IconButton
          size="small"
          color="primary"
          disabled={downloadingId === params.row.id}
          onClick={() => void handleDownload(params.row.id)}
          title={t("snapshotFiles.download")}
        >
          {downloadingId === params.row.id ? (
            <CircularProgress size={20} />
          ) : (
            <Download />
          )}
        </IconButton>
      ),
    },
  ];

  if (state.error && files.length === 0) {
    return (
      <Box p={2}>
        <Alert severity="error">{state.error}</Alert>
      </Box>
    );
  }

  return (
    <Stack spacing={3} display="flex" flexDirection="column" flex={1}>
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
      <TextField
        fullWidth
        variant="outlined"
        placeholder={t("snapshotFiles.searchPlaceholder")}
        value={searchInput}
        onChange={(e) => setSearchInput(e.target.value)}
      />
      <Box flex={1} minHeight={480}>
        <DataGrid
          rows={files}
          columns={columns}
          loading={state.loading}
          pagination
          paginationMode="server"
          filterMode="server"
          rowCount={totalCount}
          paginationMeta={{ hasNextPage }}
          paginationModel={paginationModel}
          onPaginationModelChange={handlePaginationChange}
          pageSizeOptions={[25, 50, 100, 200]}
          initialState={{
            columns: {
              columnVisibilityModel: {
                name: false,
              },
            },
          }}
          disableColumnFilter
          disableColumnSorting
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
