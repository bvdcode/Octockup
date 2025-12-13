import { Box, Card, CardContent, Divider, Typography } from "@mui/material";
import { ArrowDownward } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { BackupItem, ScheduleReport } from "../../types/api";
import { BackupStatus } from "../../types/api";
import { getBackupOverallStatus } from "../../utils/backupUtils";
import { getSourceIcon } from "../../constants/sourceIcons";
import { EditableModuleTag } from "../EditableModuleTag";
import { BackupStatusChip } from "./BackupStatusChip";
import { BackupMetadata } from "./BackupMetadata";
import { BackupProgress } from "./BackupProgress";
import { BackupActions } from "./BackupActions";
import { useSchedulesApi } from "../../api/schedulesApi";

interface BackupCardProps {
  backup: BackupItem;
  scheduleToBackupMap: Record<string, string>;
  scheduleReports: Record<string, ScheduleReport>;
  runningId: string | null;
  cancelingId: string | null;
  deletingId: string | null;
  savingIgnoredPathsId: string | null;
  onRename: (backupId: string, newTag: string) => Promise<void>;
  onEditIgnoredPaths: (backupId: string) => void;
  onRunOnce: (backupId: string, backup: BackupItem) => Promise<void>;
  onCancel: (backupId: string) => Promise<void>;
  onDelete: (backupId: string) => Promise<void>;
}

export function BackupCard({
  backup,
  scheduleToBackupMap,
  scheduleReports,
  runningId,
  cancelingId,
  deletingId,
  savingIgnoredPathsId,
  onRename,
  onEditIgnoredPaths,
  onRunOnce,
  onCancel,
  onDelete,
}: BackupCardProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const schedulesApi = useSchedulesApi();

  const status = getBackupOverallStatus(
    backup,
    scheduleToBackupMap,
    scheduleReports,
  );

  const report = Object.entries(scheduleReports).find(
    ([scheduleId, r]) =>
      scheduleToBackupMap[scheduleId] === backup.id &&
      r.status === BackupStatus.Running,
  )?.[1];

  return (
    <Card
      sx={(theme) => ({
        display: "flex",
        alignItems: "center",
        position: "relative",
        minHeight: 80,
        borderLeft: `3px solid ${
          status === "running"
            ? theme.palette.info.main
            : status === "failed"
            ? theme.palette.error.main
            : status === "warning"
            ? theme.palette.warning.main
            : status === "scheduled"
            ? theme.palette.warning.light
            : status === "success"
            ? theme.palette.success.main
            : theme.palette.grey[300]
        }`,
      })}
    >
      <CardContent
        sx={{
          display: "flex",
          alignItems: "center",
          gap: 2,
          width: "100%",
          p: 2,
          "&:last-child": { pb: 2 },
        }}
      >
        <Box
          display="flex"
          alignItems="center"
          justifyContent="center"
          flexDirection="column"
        >
          <Box
            fontSize={36}
            sx={{
              width: 36,
              height: 36,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            {getSourceIcon(backup.source.backupModuleId)}
          </Box>
          <ArrowDownward />
          <Box
            fontSize={36}
            sx={{
              width: 36,
              height: 36,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            {getSourceIcon(backup.storage.backupModuleId)}
          </Box>
        </Box>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Box
            display="flex"
            alignItems="center"
            justifyContent="space-between"
            gap={1}
          >
            <Box display="flex" alignItems="center" gap={1} minWidth={0}>
              <EditableModuleTag
                tag={backup.tag}
                onRename={(newTag) => onRename(backup.id, newTag)}
              />
              <Divider orientation="vertical" flexItem />
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                {backup.source.tag} → {backup.storage.tag}
              </Typography>
            </Box>
            <BackupStatusChip
              backup={backup}
              scheduleToBackupMap={scheduleToBackupMap}
              scheduleReports={scheduleReports}
            />
          </Box>
          <BackupMetadata backup={backup} />
          {report && <BackupProgress report={report} />}
        </Box>
        <Divider orientation="vertical" flexItem />
        <BackupActions
          backup={backup}
          scheduleToBackupMap={scheduleToBackupMap}
          scheduleReports={scheduleReports}
          runningId={runningId}
          cancelingId={cancelingId}
          deletingId={deletingId}
          savingIgnoredPathsId={savingIgnoredPathsId}
          status={status}
          onNavigateToSnapshots={() =>
            navigate(`/backups/${backup.id}/snapshots`)
          }
          onEditIgnoredPaths={() => onEditIgnoredPaths(backup.id)}
          onRunOnce={() => onRunOnce(backup.id, backup)}
          onCancel={async (scheduleId: string) => {
            await schedulesApi.cancel(scheduleId);
            await onCancel(backup.id);
          }}
          onDelete={() => onDelete(backup.id)}
        />
      </CardContent>
    </Card>
  );
}
