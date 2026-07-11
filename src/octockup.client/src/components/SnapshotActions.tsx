import {
  Box,
  CircularProgress,
  IconButton,
  Tooltip,
} from "@mui/material";
import {
  ContentCopy,
  DeleteOutline,
  Download,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { SnapshotDto } from "../types/api";

interface SnapshotActionsProps {
  snapshot: SnapshotDto;
  deleting: boolean;
  downloading: boolean;
  copying: boolean;
  onDelete: (snapshot: SnapshotDto) => Promise<void>;
  onDownload: (snapshotId: string) => Promise<void>;
  onCopyLink: (snapshotId: string) => Promise<void>;
}

export default function SnapshotActions({
  snapshot,
  deleting,
  downloading,
  copying,
  onDelete,
  onDownload,
  onCopyLink,
}: SnapshotActionsProps) {
  const { t } = useTranslation();
  const completed = Boolean(snapshot.completedAt);

  return (
    <Box display="flex" gap={0.5}>
      <Tooltip title={t("snapshots.download")}>
        <span>
          <IconButton
            size="small"
            color="primary"
            disabled={downloading || !completed}
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
            disabled={copying || !completed}
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
      <Tooltip title={t("snapshots.delete")}>
        <span>
          <IconButton
            size="small"
            color="error"
            disabled={deleting}
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
