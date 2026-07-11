import {
  Box,
  Divider,
  LinearProgress,
  Tooltip,
  Typography,
} from "@mui/material";
import { AccessTime, WarningAmber } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import {
  BackupProgressStage,
  type ScheduleReport,
} from "../../types/api";
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

const stageTranslationKeys: Record<BackupProgressStage, string> = {
  [BackupProgressStage.Listing]: "backups.progressStages.listing",
  [BackupProgressStage.Preparing]: "backups.progressStages.preparing",
  [BackupProgressStage.Reading]: "backups.progressStages.reading",
  [BackupProgressStage.Hashing]: "backups.progressStages.hashing",
  [BackupProgressStage.Compressing]: "backups.progressStages.compressing",
  [BackupProgressStage.Encrypting]: "backups.progressStages.encrypting",
  [BackupProgressStage.Uploading]: "backups.progressStages.uploading",
  [BackupProgressStage.Recording]: "backups.progressStages.recording",
  [BackupProgressStage.Persisting]: "backups.progressStages.persisting",
  [BackupProgressStage.Finalizing]: "backups.progressStages.finalizing",
  [BackupProgressStage.Completed]: "backups.progressStages.completed",
  [BackupProgressStage.Failed]: "backups.progressStages.failed",
};

export function BackupProgress({ report }: BackupProgressProps) {
  const { t } = useTranslation();
  const enumerationCompleted = report.isEnumerationCompleted;
  const progress =
    enumerationCompleted && report.total > 0
      ? (report.processed / report.total) * 100
      : 0;

  const remainingFiles = Math.max(report.total - report.processed, 0);
  const elapsedSeconds = parseElapsedToSeconds(report.elapsed);
  const etaSeconds =
    enumerationCompleted && progress > 0 && elapsedSeconds !== null
      ? elapsedSeconds * (100 / progress - 1)
      : null;
  const noProgressSeconds =
    parseElapsedToSeconds(report.noProgressFor) ?? 0;
  const showNoProgress = noProgressSeconds >= 10;
  const warnAboutNoProgress = noProgressSeconds >= 60;
  const noProgressDuration =
    noProgressSeconds >= 3600
      ? t("backups.progress.durationHoursMinutes", {
          hours: Math.floor(noProgressSeconds / 3600),
          minutes: Math.floor((noProgressSeconds % 3600) / 60),
        })
      : noProgressSeconds >= 60
        ? t("backups.progress.durationMinutesSeconds", {
            minutes: Math.floor(noProgressSeconds / 60),
            seconds: noProgressSeconds % 60,
          })
        : t("backups.progress.durationSeconds", {
            seconds: noProgressSeconds,
          });
  const stageLabel = t(stageTranslationKeys[report.stage]);
  const progressLabel = report.currentFile
    ? t("backups.progress.currentFile", {
        stage: stageLabel,
        file: report.currentFile,
      })
    : stageLabel;

  return (
    <Box sx={{ mt: 1 }}>
      <LinearProgress
        variant={enumerationCompleted ? "determinate" : "indeterminate"}
        value={enumerationCompleted ? progress : undefined}
        sx={{ height: 4, borderRadius: 1, mb: 0.5 }}
      />
      <Box
        display="flex"
        flexDirection={{ xs: "column", sm: "row" }}
        justifyContent="space-between"
        alignItems={{ xs: "stretch", sm: "center" }}
        gap={0.5}
      >
        <Tooltip
          title={report.currentPath || report.currentFile || report.message}
          placement="top-start"
          arrow
        >
          <Typography
            variant="caption"
            color="text.secondary"
            noWrap
            sx={{ flex: 1, minWidth: 0 }}
          >
            {progressLabel}
          </Typography>
        </Tooltip>
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ whiteSpace: "nowrap" }}
        >
          {enumerationCompleted
            ? t("backups.progress.completedCount", {
                processed: report.processed.toLocaleString(),
                total: report.total.toLocaleString(),
                progress: progress.toFixed(0),
              })
            : t("backups.progress.discoveredCount", {
                processed: report.processed.toLocaleString(),
                total: report.total.toLocaleString(),
              })}
        </Typography>
      </Box>
      <Box
        display="flex"
        flexDirection={{ xs: "column", sm: "row" }}
        justifyContent="space-between"
        alignItems={{ xs: "stretch", sm: "center" }}
        gap={{ xs: 0.5, sm: 2 }}
        mt={0.5}
      >
        <Box display="flex" alignItems="center" gap={1} flexWrap="wrap">
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
          {showNoProgress && (
            <Box display="flex" alignItems="center" gap={0.5}>
              {warnAboutNoProgress && (
                <WarningAmber sx={{ fontSize: 14 }} color="warning" />
              )}
              <Typography
                variant="caption"
                color={warnAboutNoProgress ? "warning.main" : "text.secondary"}
              >
                {t("backups.progress.noProgress", {
                  duration: noProgressDuration,
                })}
              </Typography>
            </Box>
          )}
        </Box>
        <Box display="flex" alignItems="center" gap={1} flexWrap="wrap">
          {enumerationCompleted ? (
            <>
              <Typography variant="caption" color="text.secondary">
                {t("backups.progress.remaining", {
                  remaining: remainingFiles.toLocaleString(),
                })}
              </Typography>
              {etaSeconds !== null && (
                <>
                  <Divider orientation="vertical" flexItem />
                  <Typography variant="caption" color="text.secondary">
                    ~{formatDurationShort(etaSeconds)}
                  </Typography>
                </>
              )}
            </>
          ) : (
            <EnumerationProgress />
          )}
        </Box>
      </Box>
    </Box>
  );
}
