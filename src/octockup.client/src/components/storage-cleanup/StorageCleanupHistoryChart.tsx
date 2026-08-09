import {
  Box,
  Card,
  CardContent,
  Stack,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { StorageCleanupRun } from "../../types/storageCleanup";
import { formatSize } from "../../utils/formatUtils";

interface StorageCleanupHistoryChartProps {
  runs: StorageCleanupRun[];
}

export default function StorageCleanupHistoryChart({
  runs,
}: StorageCleanupHistoryChartProps) {
  const { t, i18n } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const chartRuns = runs.slice(0, isMobile ? 6 : 12).reverse();
  const maximumBytes = Math.max(
    1,
    ...chartRuns.map((run) => run.reclaimedBytes),
  );

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2}>
          <Box>
            <Typography variant="h6">
              {t("storageCleanup.historyChart.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t("storageCleanup.historyChart.description")}
            </Typography>
          </Box>
          <Box
            display="flex"
            alignItems="flex-end"
            gap={1}
            height={180}
            aria-label={t("storageCleanup.historyChart.title")}
          >
            {chartRuns.map((run) => {
              const height = Math.max(
                4,
                (run.reclaimedBytes / maximumBytes) * 100,
              );
              const date = new Date(run.startedAt).toLocaleDateString(
                i18n.resolvedLanguage,
                { month: "short", day: "numeric" },
              );
              return (
                <Tooltip
                  key={run.id}
                  title={t("storageCleanup.historyChart.tooltip", {
                    storage: run.moduleTag,
                    reclaimed: formatSize(run.reclaimedBytes),
                    deleted: run.deletedChunks.toLocaleString(
                      i18n.resolvedLanguage,
                    ),
                  })}
                >
                  <Stack
                    flex={1}
                    height="100%"
                    minWidth={18}
                    justifyContent="flex-end"
                    alignItems="center"
                    spacing={0.5}
                  >
                    <Box
                      width="100%"
                      maxWidth={42}
                      height={`${height}%`}
                      bgcolor="primary.main"
                      borderRadius="4px 4px 0 0"
                    />
                    <Typography variant="caption" color="text.secondary" noWrap>
                      {date}
                    </Typography>
                  </Stack>
                </Tooltip>
              );
            })}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
