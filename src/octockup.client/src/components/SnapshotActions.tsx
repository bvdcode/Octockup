import {
  Box,
  CircularProgress,
  IconButton,
  Tooltip,
} from "@mui/material";
import {
  CancelOutlined,
  ContentCopy,
  DeleteOutline,
  Download,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import {
  SnapshotArchiveStatus,
  type SnapshotArchiveJob,
  type SnapshotDto,
} from "../types/api";
import { isSnapshotArchiveActive } from "../utils/snapshotArchiveUtils";

interface SnapshotActionsProps {
  snapshot: SnapshotDto;
  archiveJob?: SnapshotArchiveJob;
  deleting: boolean;
  downloading: boolean;
  copying: boolean;
  canceling: boolean;
  onDelete: (snapshot: SnapshotDto) => Promise<void>;
  onDownload: (snapshotId: string) => Promise<void>;
  onCopyLink: (snapshotId: string) => Promise<void>;
  onCancel: (job: SnapshotArchiveJob) => Promise<void>;
}

export default function SnapshotActions({
  snapshot,
  archiveJob,
  deleting,
  downloading,
  copying,
  canceling,
  onDelete,
  onDownload,
  onCopyLink,
  onCancel,
}: SnapshotActionsProps) {
  const { t } = useTranslation();
  const completed = Boolean(snapshot.completedAt);
  const archiveActive = isSnapshotArchiveActive(archiveJob);
  const archiveRunning = archiveJob?.status === SnapshotArchiveStatus.Running;

  return (
    <Box display="flex" gap={0.5}>
      <Tooltip title={t("snapshots.download")}>
        <span>
          <IconButton
            size="small"
            color="primary"
            disabled={downloading || archiveRunning || !completed}
            onClick={(event) => {
              event.stopPropagation();
              void onDownload(snapshot.id);
            }}
          >
            {downloading ? (
              <CircularProgress size={20} />
            ) : (
              <Download />
            )}
          </IconButton>
        </span>
      </Tooltip>
      <Tooltip title={t("snapshots.copyLink")}>
        <span>
          <IconButton
            size="small"
            color="primary"
            disabled={copying || archiveRunning || !completed}
            onClick={(event) => {
              event.stopPropagation();
              void onCopyLink(snapshot.id);
            }}
          >
            {copying ? (
              <CircularProgress size={20} />
            ) : (
              <ContentCopy />
            )}
          </IconButton>
        </span>
      </Tooltip>
      {archiveActive && archiveJob && (
        <Tooltip title={t("snapshots.archive.cancel")}>
          <span>
            <IconButton
              size="small"
              color="warning"
              disabled={canceling || archiveJob.cancellationRequested}
              onClick={(event) => {
                event.stopPropagation();
                void onCancel(archiveJob);
              }}
            >
              {canceling ? (
                <CircularProgress size={20} />
              ) : (
                <CancelOutlined />
              )}
            </IconButton>
          </span>
        </Tooltip>
      )}
      <Tooltip title={t("snapshots.delete")}>
        <span>
          <IconButton
            size="small"
            color="error"
            disabled={deleting || archiveActive}
            onClick={(event) => {
              event.stopPropagation();
              void onDelete(snapshot);
            }}
          >
            {deleting ? (
              <CircularProgress size={20} />
            ) : (
              <DeleteOutline />
            )}
          </IconButton>
        </span>
      </Tooltip>
    </Box>
  );
}
