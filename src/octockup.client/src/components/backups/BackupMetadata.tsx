import { Box, Divider, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { BackupItem } from "../../types/api";
import { formatSize } from "../../utils/formatUtils";
import { formatRelativeTime, parseUtcDate } from "../../utils/dateUtils";

interface BackupMetadataProps {
  backup: BackupItem;
}

export function BackupMetadata({ backup }: BackupMetadataProps) {
  const { t } = useTranslation();

  const lastSnapshot = backup.snapshots
    ?.filter((s) => s.completedAt)
    .sort(
      (a, b) =>
        new Date(b.completedAt!).getTime() -
        new Date(a.completedAt!).getTime(),
    )[0];

  const completedSnapshots = backup.snapshots?.filter((s) => s.completedAt);

  return (
    <Box display="flex" alignItems="center" gap={1} flexWrap="wrap" mt={0.5}>
      <Typography
        variant="caption"
        sx={{ color: "text.secondary" }}
        title={parseUtcDate(backup.createdAt)?.toLocaleString()}
      >
        {t("backups.createdAt", {
          relativeTime: formatRelativeTime(backup.createdAt, t),
        })}
      </Typography>

      {backup.snapshots && backup.snapshots.length > 0 && (
        <>
          {lastSnapshot && (
            <>
              <Divider orientation="vertical" flexItem />
              <Typography
                variant="caption"
                sx={{ color: "text.secondary" }}
                title={parseUtcDate(lastSnapshot.completedAt)?.toLocaleString()}
              >
                {t("backups.lastBackup", {
                  relativeTime: formatRelativeTime(lastSnapshot.completedAt, t),
                })}
              </Typography>
            </>
          )}
          <Divider orientation="vertical" flexItem />
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {t("backups.snapshots", {
              count: completedSnapshots?.length || 0,
            })}
          </Typography>
          {lastSnapshot && (
            <>
              <Divider orientation="vertical" flexItem />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {t("backups.totalFiles", {
                  count: lastSnapshot.filesCount,
                })}
              </Typography>
              <Divider orientation="vertical" flexItem />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {t("backups.totalSize", {
                  size: formatSize(lastSnapshot.totalSize),
                })}
              </Typography>
            </>
          )}
        </>
      )}
    </Box>
  );
}
