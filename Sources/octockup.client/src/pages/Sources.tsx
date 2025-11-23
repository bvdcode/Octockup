import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Card,
  CardContent,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  Stack,
  CircularProgress,
  Alert,
} from "@mui/material";
import { AddCircleOutline } from "@mui/icons-material";
import { getSourceIcon } from "../constants/sourceIcons";
import { useBackupSourcesApi } from "../api/backupSourcesApi";
import type { BackupSource } from "../types/api";

interface State {
  loading: boolean;
  error: string | null;
}

export function SourcesPage() {
  const api = useBackupSourcesApi();
  const [state, setState] = useState<State>({ loading: true, error: null });
  const [sources, setSources] = useState<BackupSource[]>([]);

  useEffect(() => {
    let active = true;
    api
      .list()
      .then((data) => {
        if (!active) return;
        setSources(data);
        setState({ loading: false, error: null });
      })
      .catch((e) => {
        if (!active) return;
        setState({ loading: false, error: e?.message || "Failed to load sources" });
      });
    return () => {
      active = false;
    };
  }, [api]);

  if (state.loading) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }

  if (state.error) {
    return (
      <Box p={2}>
        <Alert severity="error">{state.error}</Alert>
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h5" gutterBottom>
          Your Sources
        </Typography>
        <Card variant="outlined">
          <CardContent>
            <Typography color="text.secondary">No sources configured yet.</Typography>
          </CardContent>
        </Card>
      </Box>

      <Box>
        <Typography variant="h6" gutterBottom>
          Available Source Types
        </Typography>
        {sources.length === 0 ? (
          <Typography color="text.secondary">No available types.</Typography>
        ) : (
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>ID</TableCell>
                <TableCell>Parameters</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {sources.map((s) => (
                <TableRow key={s.id}>
                  <TableCell>{s.name}</TableCell>
                  <TableCell>{s.id}</TableCell>
                  <TableCell>{s.parameters.length > 0 ? s.parameters.join(", ") : "-"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Box>

      <Box>
        <Typography variant="h6" gutterBottom>
          Add New
        </Typography>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          {sources.map((s) => (
            <Card
              key={s.id}
              sx={{
                minWidth: 120,
                cursor: "pointer",
                "&:hover": { bgcolor: "action.hover" },
              }}
              onClick={() => {
                window.location.href = `/sources/new?type=${encodeURIComponent(s.id)}`;
              }}
            >
              <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1 }}>
                <Box sx={{ fontSize: 32 }}>{getSourceIcon(s.id)}</Box>
                <Typography variant="caption">{s.name}</Typography>
              </CardContent>
            </Card>
          ))}
          {sources.length === 0 && (
            <Stack direction="row" spacing={1} alignItems="center" color="text.secondary">
              <AddCircleOutline />
              <Typography variant="body2">No types available</Typography>
            </Stack>
          )}
        </Stack>
      </Box>
    </Stack>
  );
}

export default SourcesPage;