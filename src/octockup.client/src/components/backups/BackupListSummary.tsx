import { Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { formatSize } from "../../utils/formatUtils";
import {
  getInfoTextColor,
  getWarningTextColor,
} from "../../utils/themeColors";

interface BackupListSummaryProps {
  backupCount: number;
  issueCount: number;
  logicalSize: number;
  runningCount: number;
}

export function BackupListSummary({
  backupCount,
  issueCount,
  logicalSize,
  runningCount,
}: BackupListSummaryProps) {
  const { t } = useTranslation();

  return (
    <Stack
      component="footer"
      direction="row"
      justifyContent="center"
      spacing={2}
      flexWrap="wrap"
      py={1}
      useFlexGap
    >
      <Typography variant="caption" color="text.secondary">
        {t("backups.summary.backups", { count: backupCount })}
      </Typography>
      <Typography variant="caption" color="text.secondary">
        {t("backups.summary.logicalSize", {
          size: formatSize(logicalSize),
        })}
      </Typography>
      {runningCount > 0 && (
        <Typography variant="caption" sx={{ color: getInfoTextColor }}>
          {t("backups.summary.running", { count: runningCount })}
        </Typography>
      )}
      {issueCount > 0 && (
        <Typography variant="caption" sx={{ color: getWarningTextColor }}>
          {t("backups.summary.issues", { count: issueCount })}
        </Typography>
      )}
    </Stack>
  );
}
