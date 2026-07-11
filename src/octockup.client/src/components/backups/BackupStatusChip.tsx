import { Box, Chip, Tooltip } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { BackupItem, ScheduleReport } from "../../types/api";
import { BackupStatus } from "../../types/api";
import { getBackupOverallStatus } from "../../utils/backupUtils";

interface BackupStatusChipProps {
  backup: BackupItem;
  scheduleToBackupMap: Record<string, string>;
  scheduleReports: Map<string, ScheduleReport>;
}

export function BackupStatusChip({
  backup,
  scheduleToBackupMap,
  scheduleReports,
}: BackupStatusChipProps) {
  const { t } = useTranslation();
  const status = getBackupOverallStatus(
    backup,
    scheduleToBackupMap,
    scheduleReports,
  );

  const statusColors = {
    running: "info",
    failed: "error",
    warning: "warning",
    scheduled: "warning",
    success: "success",
    idle: "default",
    created: "default",
  } as const;

  let errorMessage = "";
  if (status === "failed") {
    const failedSchedule =
      backup.latestFinishedSchedule?.status === BackupStatus.Failed
        ? backup.latestFinishedSchedule
        : null;
    errorMessage = failedSchedule?.errorMessage || t("backups.unknownError");
  }

  const chip = (
    <Chip
      label={t(`backupStatus.${status}`)}
      size="small"
      color={statusColors[status]}
      sx={{ height: 20, fontSize: "0.7rem" }}
    />
  );

  if (status === "failed" && errorMessage) {
    return (
      <Tooltip title={errorMessage} placement="top" arrow>
        <Box component="span">{chip}</Box>
      </Tooltip>
    );
  }

  return chip;
}
