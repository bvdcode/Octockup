import {
  Box,
  LinearProgress,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { TFunction } from "i18next";
import {
  SnapshotArchivePhase,
  SnapshotArchiveStatus,
  type SnapshotArchiveJob,
} from "../types/api";
import { formatSize } from "../utils/formatUtils";
import {
  getSnapshotArchiveProgressPercent,
  isSnapshotArchiveActive,
} from "../utils/snapshotArchiveUtils";

interface SnapshotArchiveProgressProps {
  job?: SnapshotArchiveJob;
}

export default function SnapshotArchiveProgress({
  job,
}: SnapshotArchiveProgressProps) {
  const { t } = useTranslation();
  if (!job) {
    return (
      <Typography variant="body2" color="text.secondary">
        {t("snapshots.archive.notStarted")}
      </Typography>
    );
  }

  const active = isSnapshotArchiveActive(job);
  const percent = getSnapshotArchiveProgressPercent(job);
  const status = getStatusText(job, t);

  return (
    <Stack spacing={0.5} width="100%" minWidth={0} justifyContent="center">
      <Box display="flex" justifyContent="space-between" gap={1}>
        <Typography
          variant="body2"
          color={job.status === SnapshotArchiveStatus.Failed ? "error" : undefined}
        >
          {status}
        </Typography>
        {active && (
          <Typography variant="caption" color="text.secondary">
            {Math.round(percent)}%
          </Typography>
        )}
      </Box>
      {active && (
        <LinearProgress
          variant={job.totalFiles > 0 ? "determinate" : "indeterminate"}
          value={percent}
        />
      )}
      <Typography variant="caption" color="text.secondary">
        {job.phase === SnapshotArchivePhase.Preparing
          ? t("snapshots.archive.preparingProgress", {
              processed: job.processedFiles.toLocaleString(),
              total: job.totalFiles.toLocaleString(),
              references: job.preparedChunkReferences.toLocaleString(),
            })
          : job.phase === SnapshotArchivePhase.Streaming
            ? t("snapshots.archive.streamingProgress", {
                processed: job.processedFiles.toLocaleString(),
                total: job.totalFiles.toLocaleString(),
                processedSize: formatSize(job.processedBytes),
                totalSize: formatSize(job.totalBytes),
              })
            : t("snapshots.archive.waitingProgress", {
                total: job.totalFiles.toLocaleString(),
                totalSize: formatSize(job.totalBytes),
              })}
      </Typography>
      {job.currentPath && active && (
        <Tooltip title={job.currentPath}>
          <Typography
            variant="caption"
            color="text.secondary"
            noWrap
            sx={{ overflow: "hidden", textOverflow: "ellipsis" }}
          >
            {job.currentPath}
          </Typography>
        </Tooltip>
      )}
    </Stack>
  );
}

function getStatusText(job: SnapshotArchiveJob, t: TFunction): string {
  if (job.cancellationRequested && isSnapshotArchiveActive(job)) {
    return t("snapshots.archive.canceling");
  }

  if (job.status === SnapshotArchiveStatus.Completed) {
    return t("snapshots.archive.completed");
  }
  if (job.status === SnapshotArchiveStatus.Failed) {
    return t("snapshots.archive.failed");
  }
  if (job.status === SnapshotArchiveStatus.Canceled) {
    return t("snapshots.archive.canceled");
  }
  if (job.phase === SnapshotArchivePhase.Preparing) {
    return t("snapshots.archive.preparing");
  }
  if (job.phase === SnapshotArchivePhase.Streaming) {
    return t("snapshots.archive.streaming");
  }
  return t("snapshots.archive.waiting");
}
