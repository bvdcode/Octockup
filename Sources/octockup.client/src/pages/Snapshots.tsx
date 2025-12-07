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
import { useSnapshotsApi, type SnapshotDto } from "../api/snapshotsApi";

interface State {
  loading: boolean;
  error: string | null;
}

export default function SnapshotsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { backupId } = useParams<{ backupId: string }>();
  const snapshotsApi = useSnapshotsApi();
  const [state, setState] = useState<State>({
    loading: !backupId,
    error: backupId ? null : "Backup ID is missing",
  });
  const [snapshots, setSnapshots] = useState<SnapshotDto[]>([]);

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

  if (state.loading && snapshots.length === 0) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }

  if (state.error && snapshots.length === 0) {
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
          onClick={() => navigate("/backups")}
        >
          {t("common.back")}
        </Button>
        <Typography variant="h5">{t("snapshots.title")}</Typography>
      </Box>
      {snapshots.length === 0 ? (
        <Card>
          <CardContent>
            <Typography color="text.secondary">
              {t("snapshots.noSnapshots")}
            </Typography>
          </CardContent>
        </Card>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>{t("snapshots.completedAt")}</TableCell>
                <TableCell align="right">{t("snapshots.filesCount")}</TableCell>
                <TableCell align="right">{t("snapshots.totalSize")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {snapshots.map((snapshot) => (
                <TableRow
                  key={snapshot.id}
                  sx={{ "&:last-child td, &:last-child th": { border: 0 } }}
                >
                  <TableCell component="th" scope="row">
                    {snapshot.completedAt
                      ? new Date(snapshot.completedAt).toLocaleString()
                      : t("snapshots.inProgress")}
                  </TableCell>
                  <TableCell align="right">
                    {snapshot.filesCount.toLocaleString()}
                  </TableCell>
                  <TableCell align="right">
                    {formatSize(snapshot.totalSize)}
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
