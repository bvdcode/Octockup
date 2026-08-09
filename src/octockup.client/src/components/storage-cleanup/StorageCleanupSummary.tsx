import { Box, Card, CardContent, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { StorageCleanup } from "../../types/storageCleanup";
import { StorageCleanupStatus } from "../../types/storageCleanup";
import { formatSize } from "../../utils/formatUtils";

interface StorageCleanupSummaryProps {
  cleanups: StorageCleanup[];
}

export default function StorageCleanupSummary({
  cleanups,
}: StorageCleanupSummaryProps) {
  const { t, i18n } = useTranslation();
  const metrics = [
    {
      label: t("storageCleanup.summary.active"),
      value: cleanups.filter(
        (cleanup) => cleanup.status === StorageCleanupStatus.Running,
      ).length.toLocaleString(i18n.resolvedLanguage),
    },
    {
      label: t("storageCleanup.summary.pending"),
      value: cleanups
        .reduce((total, cleanup) => total + cleanup.pendingChunks, 0)
        .toLocaleString(i18n.resolvedLanguage),
    },
    {
      label: t("storageCleanup.summary.deleted"),
      value: cleanups
        .reduce((total, cleanup) => total + cleanup.totalDeletedChunks, 0)
        .toLocaleString(i18n.resolvedLanguage),
    },
    {
      label: t("storageCleanup.summary.reclaimed"),
      value: formatSize(
        cleanups.reduce(
          (total, cleanup) => total + cleanup.totalReclaimedBytes,
          0,
        ),
      ),
    },
  ];

  return (
    <Box
      display="grid"
      gridTemplateColumns={{ xs: "repeat(2, 1fr)", md: "repeat(4, 1fr)" }}
      gap={2}
    >
      {metrics.map((metric) => (
        <Card key={metric.label} variant="outlined">
          <CardContent>
            <Typography variant="body2" color="text.secondary">
              {metric.label}
            </Typography>
            <Typography variant="h5" fontWeight={600}>
              {metric.value}
            </Typography>
          </CardContent>
        </Card>
      ))}
    </Box>
  );
}
