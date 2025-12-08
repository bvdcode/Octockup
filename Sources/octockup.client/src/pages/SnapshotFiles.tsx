import {
  Box,
  Card,
  Paper,
  Stack,
  Alert,
  Table,
  Button,
  TableRow,
  TableBody,
  TableCell,
  TableHead,
  Typography,
  CardContent,
  TableContainer,
  CircularProgress,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ArrowBack } from "@mui/icons-material";
import { formatSize } from "../utils/formatUtils";
import { useNavigate, useParams } from "react-router-dom";
import { useSnapshotsApi } from "../api/snapshotsApi";
import type { SnapshotFileDto } from "../types/api";

interface State {
  loading: boolean;
  error: string | null;
}

export default function SnapshotFilesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { backupId, snapshotId } = useParams<{ backupId: string; snapshotId: string }>();
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
      {files.length === 0 ? (
        <Card>
          <CardContent>
            <Typography color="text.secondary">
              {t("snapshotFiles.noFiles")}
            </Typography>
          </CardContent>
        </Card>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>{t("snapshotFiles.name")}</TableCell>
                <TableCell>{t("snapshotFiles.path")}</TableCell>
                <TableCell align="right">{t("snapshotFiles.size")}</TableCell>
                <TableCell>{t("snapshotFiles.lastModified")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {files.map((file) => (
                <TableRow
                  key={file.id}
                  sx={{ "&:last-child td, &:last-child th": { border: 0 } }}
                >
                  <TableCell component="th" scope="row">
                    {file.name}
                  </TableCell>
                  <TableCell>{file.path}</TableCell>
                  <TableCell align="right">
                    {formatSize(file.size)}
                  </TableCell>
                  <TableCell>
                    {file.lastModified
                      ? new Date(file.lastModified).toLocaleString()
                      : "—"}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Stack>
  );
}
