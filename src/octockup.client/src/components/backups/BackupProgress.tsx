import {
  Box,
  Divider,
  LinearProgress,
  Tooltip,
  Typography,
} from "@mui/material";
import { AccessTime } from "@mui/icons-material";
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

export function BackupProgress({ report }: BackupProgressProps) {
  const progress =
    report.total > 0 ? (report.processed / report.total) * 100 : 0;

  const remainingFiles = Math.max(report.total - report.processed, 0);
  const elapsedSeconds = parseElapsedToSeconds(report.elapsed);
  const etaSeconds =
    progress > 0 && elapsedSeconds !== null
      ? elapsedSeconds * (100 / progress - 1)
      : null;

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
      <Box display="flex" justifyContent="space-between" gap={2} mt={0.5}>
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
            {remainingFiles.toLocaleString()} left{" "}
          </Typography>
          {<Divider orientation="vertical" flexItem />}
          <Typography variant="caption" color="text.secondary">
            {etaSeconds !== null && `~${formatDurationShort(etaSeconds)}`}
          </Typography>
          {report && !report.isEnumerationCompleted && (
            <Box
              sx={{
                bottom: 8,
                right: 8,
                zIndex: 1,
              }}
            >
              <EnumerationProgress />
            </Box>
          )}
        </Box>
      </Box>
    </Box>
  );
}
