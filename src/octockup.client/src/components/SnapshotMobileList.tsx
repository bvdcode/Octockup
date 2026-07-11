import { Box, Card, CardContent, Divider, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type {
  SnapshotArchiveJob,
  SnapshotDto,
} from "../types/api";
import { formatSize } from "../utils/formatUtils";
import SnapshotActions from "./SnapshotActions";
import SnapshotArchiveProgress from "./SnapshotArchiveProgress";

interface SnapshotMobileListProps {
  snapshots: SnapshotDto[];
  jobsBySnapshot: Record<string, SnapshotArchiveJob>;
  deletingId: string | null;
  downloadingId: string | null;
  copyingId: string | null;
  cancelingJobId: string | null;
  onOpen: (snapshotId: string) => void;
  onDelete: (snapshot: SnapshotDto) => Promise<void>;
  onDownload: (snapshotId: string) => Promise<void>;
  onCopyLink: (snapshotId: string) => Promise<void>;
  onCancel: (job: SnapshotArchiveJob) => Promise<void>;
}

export default function SnapshotMobileList({
  snapshots,
  jobsBySnapshot,
  deletingId,
  downloadingId,
  copyingId,
  cancelingJobId,
  onOpen,
  onDelete,
  onDownload,
  onCopyLink,
  onCancel,
}: SnapshotMobileListProps) {
  const { t } = useTranslation();

  return (
    <Stack spacing={1.5}>
      {snapshots.map((snapshot) => {
        const job = jobsBySnapshot[snapshot.id];
        return (
          <Card key={snapshot.id} variant="outlined">
            <CardContent>
              <Stack spacing={1.5}>
                <Box
                  display="flex"
                  justifyContent="space-between"
                  gap={2}
                  onClick={() => onOpen(snapshot.id)}
                  sx={{ cursor: "pointer" }}
                >
                  <Box minWidth={0}>
                    <Typography variant="caption" color="text.secondary">
                      {t("snapshots.completedAt")}
                    </Typography>
                    <Typography variant="body2">
                      {snapshot.completedAt
                        ? new Date(snapshot.completedAt).toLocaleString()
                        : t("snapshots.never")}
                    </Typography>
                  </Box>
                  <Box textAlign="right" flexShrink={0}>
                    <Typography variant="body2">
                      {snapshot.filesCount.toLocaleString()} {t("snapshots.files")}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {formatSize(snapshot.totalSize)}
                    </Typography>
                  </Box>
                </Box>
                <SnapshotArchiveProgress job={job} />
                <Divider />
                <Box display="flex" justifyContent="flex-end">
                  <SnapshotActions
                    snapshot={snapshot}
                    archiveJob={job}
                    deleting={deletingId === snapshot.id}
                    downloading={downloadingId === snapshot.id}
                    copying={copyingId === snapshot.id}
                    canceling={cancelingJobId === job?.jobId}
                    onDelete={onDelete}
                    onDownload={onDownload}
                    onCopyLink={onCopyLink}
                    onCancel={onCancel}
                  />
                </Box>
              </Stack>
            </CardContent>
          </Card>
        );
      })}
    </Stack>
  );
}
