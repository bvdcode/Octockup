import {
  Box,
  LinearProgress,
  Tooltip,
  Typography,
} from "@mui/material";
import { AccessTime } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { ScheduleReport } from "../../types/api";
import { formatSpeed, formatElapsed } from "../../utils/formatUtils";
import { EnumerationProgress } from "./EnumerationProgress";

interface BackupProgressProps {
  report: ScheduleReport;
}

export function BackupProgress({ report }: BackupProgressProps) {
  const { t } = useTranslation();
  const progress =
    report.total > 0 ? (report.processed / report.total) * 100 : 0;

  return (
    <Box sx={{ mt: 1 }}>
      {!report.isEnumerationCompleted && <EnumerationProgress />}
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
          {report.processed.toLocaleString()} /{" "}
          {report.total.toLocaleString()} • {progress.toFixed(0)}%
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
