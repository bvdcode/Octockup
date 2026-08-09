import {
  Card,
  Chip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { StorageCleanupRun } from "../../types/storageCleanup";
import { formatSize } from "../../utils/formatUtils";
import {
  formatStorageCleanupDuration,
  getRunDurationSeconds,
  getStorageCleanupStatusColor,
  getStorageCleanupStatusKey,
} from "./storageCleanupPresentation";

interface StorageCleanupRunHistoryProps {
  runs: StorageCleanupRun[];
}

export default function StorageCleanupRunHistory({
  runs,
}: StorageCleanupRunHistoryProps) {
  const { t, i18n } = useTranslation();

  return (
    <TableContainer component={Card} variant="outlined">
      <Table size="small" sx={{ minWidth: 720 }}>
        <TableHead>
          <TableRow>
            <TableCell>{t("storageCleanup.history.started")}</TableCell>
            <TableCell>{t("storageCleanup.history.storage")}</TableCell>
            <TableCell>{t("storageCleanup.history.status")}</TableCell>
            <TableCell align="right">
              {t("storageCleanup.history.duration")}
            </TableCell>
            <TableCell align="right">
              {t("storageCleanup.history.scanned")}
            </TableCell>
            <TableCell align="right">
              {t("storageCleanup.history.deleted")}
            </TableCell>
            <TableCell align="right">
              {t("storageCleanup.history.reclaimed")}
            </TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {runs.map((run) => (
            <TableRow key={run.id} hover>
              <TableCell>
                {new Date(run.startedAt).toLocaleString(i18n.resolvedLanguage)}
              </TableCell>
              <TableCell>{run.moduleTag}</TableCell>
              <TableCell>
                <Chip
                  size="small"
                  label={t(getStorageCleanupStatusKey(run.status))}
                  color={getStorageCleanupStatusColor(run.status)}
                />
              </TableCell>
              <TableCell align="right">
                {formatStorageCleanupDuration(
                  getRunDurationSeconds(run.startedAt, run.completedAt),
                  t,
                )}
              </TableCell>
              <TableCell align="right">
                {run.scannedChunks.toLocaleString(i18n.resolvedLanguage)}
              </TableCell>
              <TableCell align="right">
                {run.deletedChunks.toLocaleString(i18n.resolvedLanguage)}
              </TableCell>
              <TableCell align="right">
                {formatSize(run.reclaimedBytes)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
