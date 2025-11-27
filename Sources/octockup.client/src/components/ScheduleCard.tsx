import {
  Box,
  Card,
  Chip,
  Tooltip,
  Divider,
  Typography,
  IconButton,
  CardContent,
  LinearProgress,
} from "@mui/material";
import {
  StopCircle,
  DeleteOutline,
  ArrowRightAlt,
} from "@mui/icons-material";
import { BackupStatus } from "../types/api";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import type { ScheduleItem, ScheduleReport } from "../types/api";
import { parseUtcDate } from "../utils/dateUtils";
import { formatSpeed, formatElapsed } from "../utils/formatUtils";
import { statusColor, formatNextRun, parseInterval } from "../utils/scheduleUtils";
import { getSourceIcon } from "../constants/sourceIcons";

interface ScheduleCardProps {
  item: ScheduleItem;
  report?: ScheduleReport;
  onDelete: (id: string) => Promise<void>;
  onCancel: (id: string) => Promise<void>;
  isDeleting: boolean;
  isCanceling: boolean;
}

export function ScheduleCard({
  item,
  report,
  onDelete,
  onCancel,
  isDeleting,
  isCanceling,
}: ScheduleCardProps) {
  const { t } = useTranslation();
  const progress = report && report.total > 0
    ? (report.processed / report.total) * 100
    : 0;

  const handleDelete = async (e: React.MouseEvent) => {
    e.stopPropagation();
    const result = await confirm({
      title: t("schedules.deleteTitle", {
        defaultValue: "Delete schedule",
      }),
      description: t("schedules.deleteText", {
        defaultValue: "This action is permanent!",
      }),
      confirmationText: t("common.delete", {
        defaultValue: "Delete",
      }),
      cancellationText: t("common.cancel", {
        defaultValue: "Cancel",
      }),
      confirmationButtonProps: { color: "error" },
    });
    if (result.confirmed) {
      await onDelete(item.id);
    }
  };

  const handleCancel = async (e: React.MouseEvent) => {
    e.stopPropagation();
    await onCancel(item.id);
  };

  const renderIntervalInfo = () => {
    if (!item.interval) return "";
    
    const minutes = parseInterval(item.interval);
    return ` • ${t("schedules.everyMinutes", { count: minutes })}`;
  };

  return (
    <Card sx={{ display: "flex" }}>
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
        <ScheduleIcons
          sourceIcon={getSourceIcon(item.backup.source.backupModuleId)}
          storageIcon={getSourceIcon(item.backup.storage.backupModuleId)}
        />
        
        <ScheduleInfo
          item={item}
          report={report}
          progress={progress}
        />
        
        <ScheduleStatus
          item={item}
          renderIntervalInfo={renderIntervalInfo}
        />
        
        <Divider orientation="vertical" flexItem sx={{ mx: 1 }} />
        
        <ScheduleActions
          item={item}
          isDeleting={isDeleting}
          isCanceling={isCanceling}
          onDelete={handleDelete}
          onCancel={handleCancel}
        />
      </CardContent>
    </Card>
  );
}

function ScheduleIcons({
  sourceIcon,
  storageIcon,
}: {
  sourceIcon: React.ReactNode;
  storageIcon: React.ReactNode;
}) {
  return (
    <Box display="flex" alignItems="center" justifyContent="center">
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
        {sourceIcon}
      </Box>
      <ArrowRightAlt />
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
        {storageIcon}
      </Box>
    </Box>
  );
}

function ScheduleInfo({
  item,
  report,
  progress,
}: {
  item: ScheduleItem;
  report?: ScheduleReport;
  progress: number;
}) {
  return (
    <Box sx={{ flex: 1, minWidth: 0 }}>
      <Typography variant="subtitle1" noWrap title={item.backup.tag}>
        {item.backup.tag}
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary" }}>
        {item.backup.source.tag} → {item.backup.storage.tag}
      </Typography>
      {report && item.status === BackupStatus.Running && (
        <RunningProgressInfo
          report={report}
          progress={progress}
        />
      )}
    </Box>
  );
}

function RunningProgressInfo({
  report,
  progress,
}: {
  report: ScheduleReport;
  progress: number;
}) {
  return (
    <Box sx={{ mt: 1 }}>
      <LinearProgress
        variant="determinate"
        value={progress}
        sx={{ height: 4, borderRadius: 1, mb: 0.5 }}
      />
      <Box display="flex" justifyContent="space-between" alignItems="center">
        <Typography
          variant="caption"
          color="text.secondary"
          noWrap
          sx={{ flex: 1, mr: 1 }}
        >
          {report.message}
        </Typography>
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ whiteSpace: "nowrap" }}
        >
          {report.processed.toLocaleString()} / {report.total.toLocaleString()}{" "}
          • {progress.toFixed(0)}%
        </Typography>
      </Box>
      <Box display="flex" gap={2} mt={0.5}>
        <Typography variant="caption" color="text.secondary">
          {formatSpeed(report.speed)}
        </Typography>
        {report.elapsed && (
          <Typography variant="caption" color="text.secondary">
            ⏱ {formatElapsed(report.elapsed)}
          </Typography>
        )}
      </Box>
    </Box>
  );
}

function ScheduleStatus({
  item,
  renderIntervalInfo,
}: {
  item: ScheduleItem;
  renderIntervalInfo: () => string;
}) {
  const { t } = useTranslation();
  
  return (
    <Box
      display="flex"
      flexDirection="column"
      gap={0.5}
      alignItems="flex-end"
      minWidth={120}
    >
      <Chip
        size="small"
        label={t(
          `schedules.status.${BackupStatus[item.status].toLowerCase()}`,
        )}
        color={statusColor(item.status)}
      />
      {item.status !== BackupStatus.Running && (
        <>
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {t("schedules.nextRun.label", {
              defaultValue: "Next run",
            })}
            : {formatNextRun(item, t)}
          </Typography>
          <Typography
            variant="caption"
            sx={{ color: "text.secondary", fontSize: "0.65rem" }}
          >
            {parseUtcDate(item.startAt)!.toLocaleString()}
            {renderIntervalInfo()}
          </Typography>
        </>
      )}
    </Box>
  );
}

function ScheduleActions({
  item,
  isDeleting,
  isCanceling,
  onDelete,
  onCancel,
}: {
  item: ScheduleItem;
  isDeleting: boolean;
  isCanceling: boolean;
  onDelete: (e: React.MouseEvent) => Promise<void>;
  onCancel: (e: React.MouseEvent) => Promise<void>;
}) {
  const { t } = useTranslation();
  
  return (
    <Box display="flex" flexDirection="column" gap={0.5}>
      <Tooltip
        title={t("schedules.deleteTooltip", {
          defaultValue: "Delete schedule",
        })}
        placement="top"
      >
        <span>
          <IconButton
            size="small"
            aria-label={t("common.delete")}
            disabled={isDeleting}
            onClick={onDelete}
          >
            <DeleteOutline color="primary" />
          </IconButton>
        </span>
      </Tooltip>
      <Tooltip
        title={t("schedules.stopTooltip", {
          defaultValue: "Stop running schedule",
        })}
        placement="top"
      >
        <span>
          <IconButton
            size="small"
            aria-label={t("schedules.stop", {
              defaultValue: "Stop",
            })}
            disabled={
              item.status !== BackupStatus.Running || isCanceling
            }
            onClick={onCancel}
          >
            <StopCircle />
          </IconButton>
        </span>
      </Tooltip>
    </Box>
  );
}
