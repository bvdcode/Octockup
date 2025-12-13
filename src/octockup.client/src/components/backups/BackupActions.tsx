import { Box, CircularProgress, IconButton, Tooltip } from "@mui/material";
import {
  BackupTable,
  DeleteOutline,
  FilterAlt,
  PlayArrow,
  StopCircle,
} from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { confirm } from "material-ui-confirm";
import type { BackupItem, ScheduleReport } from "../../types/api";
import { BackupStatus } from "../../types/api";

interface BackupActionsProps {
  backup: BackupItem;
  scheduleToBackupMap: Record<string, string>;
  scheduleReports: Record<string, ScheduleReport>;
  runningId: string | null;
  cancelingId: string | null;
  deletingId: string | null;
  savingIgnoredPathsId: string | null;
  status: string;
  onNavigateToSnapshots: () => void;
  onEditIgnoredPaths: () => void;
  onRunOnce: () => Promise<void>;
  onCancel: (scheduleId: string) => Promise<void>;
  onDelete: () => Promise<void>;
}

export function BackupActions({
  backup,
  scheduleToBackupMap,
  scheduleReports,
  runningId,
  cancelingId,
  deletingId,
  savingIgnoredPathsId,
  status,
  onNavigateToSnapshots,
  onEditIgnoredPaths,
  onRunOnce,
  onCancel,
  onDelete,
}: BackupActionsProps) {
  const { t } = useTranslation();

  const runningSchedule = Object.entries(scheduleReports).find(
    ([scheduleId, r]) =>
      scheduleToBackupMap[scheduleId] === backup.id &&
      r.status === BackupStatus.Running,
  );
  const isRunning = !!runningSchedule;

  return (
    <Box display="flex" flexDirection="column">
      <Tooltip title={t("backups.showSnapshots")} placement="left">
        <IconButton
          size="small"
          aria-label={t("backups.showSnapshots")}
          onClick={onNavigateToSnapshots}
        >
          <BackupTable color="warning" />
        </IconButton>
      </Tooltip>
      <Tooltip title={t("backups.ignoredPaths")} placement="left">
        <IconButton
          size="small"
          aria-label={t("backups.ignoredPaths")}
          disabled={savingIgnoredPathsId === backup.id}
          onClick={(e) => {
            e.stopPropagation();
            onEditIgnoredPaths();
          }}
        >
          <FilterAlt color="info" />
        </IconButton>
      </Tooltip>
      {isRunning ? (
        <Tooltip title={t("backups.stop")} placement="left">
          <IconButton
            size="small"
            aria-label={t("backups.stop")}
            disabled={cancelingId === backup.id}
            onClick={async (e) => {
              e.stopPropagation();
              const scheduleId = runningSchedule[0];
              await onCancel(scheduleId);
            }}
          >
            {cancelingId === backup.id ? (
              <CircularProgress size={20} />
            ) : (
              <StopCircle color="error" />
            )}
          </IconButton>
        </Tooltip>
      ) : (
        <Tooltip title={t("backups.runOnce")} placement="left">
          <IconButton
            size="small"
            aria-label={t("backups.runOnce")}
            disabled={runningId === backup.id || status === "running"}
            onClick={async (e) => {
              e.stopPropagation();
              await onRunOnce();
            }}
          >
            {runningId === backup.id ? (
              <CircularProgress size={20} />
            ) : (
              <PlayArrow color="success" />
            )}
          </IconButton>
        </Tooltip>
      )}
      <Tooltip title={t("backups.deleteTooltip")} placement="left">
        <IconButton
          size="small"
          aria-label={t("common.delete")}
          disabled={deletingId === backup.id}
          onClick={async (e) => {
            e.stopPropagation();
            const result = await confirm({
              title: t("backups.deleteTitle"),
              description: t("backups.deleteText"),
              confirmationText: t("common.delete"),
              cancellationText: t("common.cancel"),
              confirmationButtonProps: { color: "error" },
            });
            if (result.confirmed) {
              await onDelete();
            }
          }}
        >
          <DeleteOutline color="primary" />
        </IconButton>
      </Tooltip>
    </Box>
  );
}
