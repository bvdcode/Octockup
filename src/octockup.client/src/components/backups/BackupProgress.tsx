import {
  Box,
  Divider,
  LinearProgress,
  Tooltip,
  Typography,
} from "@mui/material";
import { AccessTime } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { ScheduleReport } from "../../types/api";
import {
  formatSpeed,
  formatElapsed,
  parseElapsedToSeconds,
  formatDurationShort,
} from "../../utils/formatUtils";
import { EnumerationProgress } from "./EnumerationProgress";

interface BackupProgressProps {
  report: ScheduleReport;
}

interface BackupProgressSummary {
  progress: number;
  remainingFiles: number;
  etaSeconds: number | null;
}

function getProgressSummary(report: ScheduleReport): BackupProgressSummary {
  const progress =
    report.total > 0 ? (report.processed / report.total) * 100 : 0;
  const elapsedSeconds = parseElapsedToSeconds(report.elapsed);
  return {
    progress,
    remainingFiles: Math.max(report.total - report.processed, 0),
    etaSeconds:
      progress > 0 && elapsedSeconds !== null
        ? elapsedSeconds * (100 / progress - 1)
        : null,
  };
}

export function BackupProgress({ report }: BackupProgressProps) {
  const summary = getProgressSummary(report);

  return (
    <Box sx={{ mt: 1 }}>
      <LinearProgress
        variant="determinate"
        value={summary.progress}
        sx={{ height: 4, borderRadius: 1, mb: 0.5 }}
      />
      <ProgressHeading report={report} progress={summary.progress} />
      <ProgressDetails report={report} summary={summary} />
    </Box>
  );
}

function ProgressHeading({
  report,
  progress,
}: BackupProgressProps & { progress: number }) {
  return (
    <Box display="flex" justifyContent="space-between" alignItems="center">
      <Tooltip
        title={report.currentPath || report.currentFile || report.message}
        placement="top-start"
        arrow
      >
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
        {report.processed.toLocaleString()} / {report.total.toLocaleString()} •{" "}
        {progress.toFixed(0)}%
      </Typography>
    </Box>
  );
}

function ProgressDetails({
  report,
  summary,
}: BackupProgressProps & { summary: BackupProgressSummary }) {
  const { t } = useTranslation();
  return (
    <Box
      display="flex"
      justifyContent="space-between"
      alignItems="center"
      gap={2}
      mt={0.5}
    >
      <Box display="flex" alignItems="center" gap={1}>
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
      <Box display="flex" alignItems="center" gap={1}>
        <Typography variant="caption" color="text.secondary">
          {t("backups.remaining", {
            value: summary.remainingFiles.toLocaleString(),
          })}
        </Typography>
        <Divider orientation="vertical" flexItem />
        <Typography variant="caption" color="text.secondary">
          {summary.etaSeconds === null
            ? null
            : `~${formatDurationShort(summary.etaSeconds)}`}
        </Typography>
        {!report.isEnumerationCompleted && (
          <>
            <Divider orientation="vertical" flexItem />
            <EnumerationProgress />
          </>
        )}
      </Box>
    </Box>
  );
}
