import {
  Box,
  Card,
  Tooltip,
  Divider,
  Typography,
  IconButton,
  CardContent,
  LinearProgress,
} from "@mui/material";
import {
  Replay,
  Pending,
  AccessTime,
  StopCircle,
  CheckCircle,
  ErrorOutline,
  DeleteOutline,
  ArrowRightAlt,
  ArrowDownward,
  HourglassEmpty,
} from "@mui/icons-material";
import { BackupStatus } from "../types/api";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import type { ScheduleItem, ScheduleReport } from "../types/api";
import { formatSpeed, formatElapsed } from "../utils/formatUtils";
import {
  calculateNextRunTime,
  formatNextRun,
  formatInterval,
} from "../utils/scheduleUtils";
import { getSourceIcon } from "../constants/sourceIcons";

function getStatusIcon(status: BackupStatus) {
  switch (status) {
    case BackupStatus.Completed:
      return <CheckCircle sx={{ color: "success.main" }} />;
    case BackupStatus.Failed:
      return <ErrorOutline sx={{ color: "error.main" }} />;
    case BackupStatus.Running:
      return <Pending sx={{ color: "info.main" }} />;
    case BackupStatus.Created:
    default:
      return <HourglassEmpty sx={{ color: "warning.main" }} />;
  }
}

interface ScheduleCardProps {
  item: ScheduleItem;
  report?: ScheduleReport;
  onDelete: (id: string) => Promise<void>;
  onCancel: (id: string) => Promise<void>;
  onResetError: (id: string) => Promise<void>;
  isDeleting: boolean;
  isCanceling: boolean;
  isResetting: boolean;
}

export function ScheduleCard({
  item,
  report,
  onDelete,
  onCancel,
  onResetError,
  isDeleting,
  isCanceling,
  isResetting,
}: ScheduleCardProps) {
  const { t } = useTranslation();
  const progress =
    report && report.total > 0 ? (report.processed / report.total) * 100 : 0;

  const handleDelete = async (e: React.MouseEvent) => {
    e.stopPropagation();
    const result = await confirm({
      title: t("schedules.deleteTitle"),
      description: t("schedules.deleteText"),
      confirmationText: t("common.delete"),
      cancellationText: t("common.cancel"),
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

  return (
    <Card
      sx={(theme) => ({
        display: "flex",
        borderLeft: `3px solid ${
          item.status === BackupStatus.Completed
            ? theme.palette.success.main
            : item.status === BackupStatus.Failed
            ? theme.palette.error.main
            : item.status === BackupStatus.Running
            ? theme.palette.info.main
            : theme.palette.warning.main
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
        <ScheduleIcons
          sourceIcon={getSourceIcon(item.backup.source.backupModuleId)}
          storageIcon={getSourceIcon(item.backup.storage.backupModuleId)}
        />

        <ScheduleInfo item={item} report={report} progress={progress} />

        <Divider orientation="vertical" flexItem />

        <ScheduleActions
          item={item}
          isDeleting={isDeleting}
          isCanceling={isCanceling}
          isResetting={isResetting}
          onDelete={handleDelete}
          onCancel={handleCancel}
          onResetError={onResetError}
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
    <Box
      display="flex"
      alignItems="center"
      justifyContent="center"
      flexDirection={{
        xs: "column",
        sm: "row",
      }}
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
        {sourceIcon}
      </Box>
      <ArrowRightAlt
        sx={{
          display: { xs: "none", sm: "block" },
          mx: 1,
          my: { xs: 1, sm: 0 },
        }}
      />
      <ArrowDownward
        sx={{
          display: { xs: "block", sm: "none" },
        }}
      />
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
  const { t } = useTranslation();
  const nextRun = calculateNextRunTime(item);

  const renderIntervalInfo = () => {
    if (!item.interval) {
      return "";
    }
    return ` • ${formatInterval(item.interval, t)}`;
  };
  return (
    <Box
      sx={{
        flex: 1,
        minWidth: 0,
      }}
    >
      <Typography variant="subtitle1" noWrap title={item.backup.tag}>
        {item.backup.tag}
      </Typography>
      <Typography variant="caption" sx={{ color: "text.secondary" }}>
        {item.backup.source.tag} → {item.backup.storage.tag}
      </Typography>
      {report && item.status === BackupStatus.Running && (
        <RunningProgressInfo report={report} progress={progress} />
      )}
      <Box sx={{ mt: 0.5 }}>
        {item.status !== BackupStatus.Running && (
          <Box display="flex" alignItems="center" gap={0.5}>
            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                display: "block",
                fontSize: "0.7rem",
              }}
            >
              {t("schedules.nextRun.label")}: {formatNextRun(item, t)}
            </Typography>

            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                display: "block",
                fontSize: "0.7rem",
              }}
            >
              {renderIntervalInfo()}
            </Typography>
          </Box>
        )}
        {nextRun && (
          <Typography
            variant="caption"
            sx={{
              color: "text.secondary",
              display: "block",
              fontSize: "0.7rem",
            }}
          >
            {nextRun.toLocaleString()}
          </Typography>
        )}
      </Box>
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
        <Tooltip title={report.message} placement="top" arrow>
          <Typography
            variant="caption"
            color="text.secondary"
            noWrap
            sx={{ flex: 1, mr: 1 }}
          >
            {report.message}
          </Typography>
        </Tooltip>
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
          <Box display="flex" alignItems="center" gap={0.5}>
            <AccessTime sx={{ fontSize: 14 }} color="action" />
            <Typography variant="caption" color="text.secondary">
              {formatElapsed(report.elapsed)}
            </Typography>
          </Box>
        )}
      </Box>
    </Box>
  );
}

function ScheduleActions({
  item,
  isDeleting,
  isCanceling,
  isResetting,
  onDelete,
  onCancel,
  onResetError,
}: {
  item: ScheduleItem;
  isDeleting: boolean;
  isCanceling: boolean;
  isResetting: boolean;
  onDelete: (e: React.MouseEvent) => Promise<void>;
  onCancel: (e: React.MouseEvent) => Promise<void>;
  onResetError: (id: string) => Promise<void>;
}) {
  const { t } = useTranslation();

  const handleResetError = async (e: React.MouseEvent) => {
    e.stopPropagation();
    await onResetError(item.id);
  };

  const statusLabel = t(
    `schedules.status.${BackupStatus[item.status].toLowerCase()}`,
  );

  return (
    <Box display="flex" flexDirection="column" gap={0.5} alignItems="center">
      <Tooltip
        title={statusLabel}
        placement="left"
        onClick={() => {
          confirm({
            title: t("schedules.currentStatus"),
            description:
              statusLabel + (item.errorMessage ? `: ${item.errorMessage}` : ""),
            hideCancelButton: true,
            confirmationText: t("common.ok"),
          });
        }}
      >
        <Box sx={{ display: "flex", alignItems: "center", cursor: "pointer" }}>
          {getStatusIcon(item.status)}
        </Box>
      </Tooltip>

      <Tooltip title={t("schedules.deleteTooltip")} placement="left">
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
      {item.status === BackupStatus.Failed ? (
        <Tooltip title={t("schedules.tryAgainTooltip")} placement="left">
          <span>
            <IconButton
              size="small"
              aria-label={t("schedules.tryAgain")}
              disabled={isResetting}
              onClick={handleResetError}
            >
              <Replay color="warning" />
            </IconButton>
          </span>
        </Tooltip>
      ) : (
        <Tooltip title={t("schedules.stopTooltip")} placement="left">
          <span>
            <IconButton
              size="small"
              aria-label={t("schedules.stop")}
              disabled={item.status !== BackupStatus.Running || isCanceling}
              onClick={onCancel}
            >
              <StopCircle />
            </IconButton>
          </span>
        </Tooltip>
      )}
    </Box>
  );
}
