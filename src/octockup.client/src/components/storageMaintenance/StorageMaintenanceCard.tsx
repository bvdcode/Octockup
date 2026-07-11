import {
  Box,
  Card,
  Chip,
  Alert,
  Stack,
  Button,
  Tooltip,
  Divider,
  Typography,
  AlertTitle,
  CardContent,
  LinearProgress,
  CircularProgress,
} from "@mui/material";
import { Cancel, CleaningServices, Storage } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import {
  StorageCleanupPhase,
  StorageCleanupStatus,
  type StorageCleanupJob,
  type StorageMaintenanceSummary,
} from "../../types/api";
import { formatSize } from "../../utils/formatUtils";

interface StorageMaintenanceCardProps {
  storage: StorageMaintenanceSummary;
  job?: StorageCleanupJob;
  starting: boolean;
  canceling: boolean;
  statsLoading: boolean;
  onStart: () => Promise<void>;
  onCancel: (jobId: string) => Promise<void>;
}

function formatCount(value: number | null | undefined, loadingLabel: string) {
  return value === null || value === undefined
    ? loadingLabel
    : value.toLocaleString();
}

function formatBytes(value: number | null | undefined, loadingLabel: string) {
  return value === null || value === undefined
    ? loadingLabel
    : formatSize(value);
}

function formatReferencedChunks(
  value: number | null | undefined,
  loading: boolean,
  loadingLabel: string,
  notScannedLabel: string,
) {
  if (value !== null && value !== undefined) {
    return value.toLocaleString();
  }

  return loading ? loadingLabel : notScannedLabel;
}

function isActiveJob(job?: StorageCleanupJob): boolean {
  return (
    job?.status === StorageCleanupStatus.Pending ||
    job?.status === StorageCleanupStatus.Running
  );
}

function getStatusColor(
  status: StorageCleanupStatus,
): "default" | "primary" | "success" | "error" | "warning" {
  if (status === StorageCleanupStatus.Completed) return "success";
  if (status === StorageCleanupStatus.Failed) return "error";
  if (status === StorageCleanupStatus.Canceled) return "warning";
  if (status === StorageCleanupStatus.Running) return "primary";
  return "default";
}

export function StorageMaintenanceCard({
  storage,
  job,
  starting,
  canceling,
  statsLoading,
  onStart,
  onCancel,
}: StorageMaintenanceCardProps) {
  const { t } = useTranslation();
  const active = isActiveJob(job);
  const loadingLabel = t("storageMaintenance.metrics.loading");
  const notScannedLabel = t("storageMaintenance.metrics.notScanned");
  const referencedChunks = job
    ? job.referencedChunks
    : storage.referencedChunks;

  const statusLabel = job
    ? t(
        `storageMaintenance.status.${StorageCleanupStatus[
          job.status
        ].toLowerCase()}`,
      )
    : t("storageMaintenance.status.notStarted");
  const phaseLabel = job
    ? t(
        `storageMaintenance.phase.${StorageCleanupPhase[
          job.phase
        ].toLowerCase()}`,
      )
    : null;

  return (
    <Card>
      <CardContent>
        <Stack spacing={2}>
          <Box display="flex" justifyContent="space-between" gap={2}>
            <Box display="flex" alignItems="center" gap={1} minWidth={0}>
              <Storage color="primary" />
              <Box minWidth={0}>
                <Typography variant="h6" noWrap>
                  {storage.tag}
                </Typography>
                <Typography variant="caption" color="text.secondary" noWrap>
                  {storage.backupModuleId}
                </Typography>
              </Box>
            </Box>
            <Chip
              size="small"
              color={job ? getStatusColor(job.status) : "default"}
              label={statusLabel}
            />
          </Box>

          <Box
            display="grid"
            gridTemplateColumns={{
              xs: "1fr 1fr",
              md: "repeat(4, minmax(0, 1fr))",
            }}
            gap={1.5}
          >
            <Metric
              label={t("storageMaintenance.metrics.indexedObjects")}
              value={formatCount(storage.indexedObjects, loadingLabel)}
            />
            <Metric
              label={t("storageMaintenance.metrics.indexedSize")}
              value={formatBytes(storage.indexedStoredSize, loadingLabel)}
            />
            <Metric
              label={t("storageMaintenance.metrics.referencedChunks")}
              value={formatReferencedChunks(
                referencedChunks,
                statsLoading,
                loadingLabel,
                notScannedLabel,
              )}
            />
            <Metric
              label={t("storageMaintenance.metrics.backups")}
              value={formatCount(storage.totalBackups, loadingLabel)}
            />
            {storage.availableBytes !== null &&
              storage.availableBytes !== undefined && (
                <Metric
                  label={t("storageMaintenance.metrics.available")}
                  value={formatSize(storage.availableBytes)}
                />
              )}
            {storage.totalCapacityBytes !== null &&
              storage.totalCapacityBytes !== undefined && (
                <Metric
                  label={t("storageMaintenance.metrics.capacity")}
                  value={formatSize(storage.totalCapacityBytes)}
                />
              )}
          </Box>
          {statsLoading && <LinearProgress />}

          {job && (
            <>
              <Divider />
              <Stack spacing={1}>
                {job.status === StorageCleanupStatus.Failed && (
                  <Alert severity="error">
                    <AlertTitle>
                      {t("storageMaintenance.failureTitle")}
                    </AlertTitle>
                    {phaseLabel && (
                      <Typography variant="caption" component="div">
                        {t("storageMaintenance.failurePhase", {
                          phase: phaseLabel,
                        })}
                      </Typography>
                    )}
                    <Typography variant="body2">
                      {job.errorMessage ||
                        t("storageMaintenance.unknownError")}
                    </Typography>
                  </Alert>
                )}
                {active && <LinearProgress />}
                {active && phaseLabel && (
                  <Typography variant="caption" color="text.secondary">
                    {job.phase === StorageCleanupPhase.CollectingReferences
                      ? t("storageMaintenance.referenceScanProgress", {
                          count: job.snapshotFilesScanned,
                        })
                      : phaseLabel}
                  </Typography>
                )}
                <Box
                  display="grid"
                  gridTemplateColumns={{
                    xs: "1fr 1fr",
                    md: "repeat(4, minmax(0, 1fr))",
                  }}
                  gap={1.5}
                >
                  <Metric
                    label={t("storageMaintenance.metrics.scannedObjects")}
                    value={job.storageObjectsScanned.toLocaleString()}
                  />
                  <Metric
                    label={t("storageMaintenance.metrics.scannedSize")}
                    value={formatSize(job.storageBytesScanned)}
                  />
                  <Metric
                    label={t("storageMaintenance.metrics.orphans")}
                    value={job.orphanObjects.toLocaleString()}
                  />
                  <Metric
                    label={t("storageMaintenance.metrics.freed")}
                    value={formatSize(job.freedBytes)}
                  />
                  <Metric
                    label={t("storageMaintenance.metrics.deleted")}
                    value={job.deletedObjects.toLocaleString()}
                  />
                  <Metric
                    label={t("storageMaintenance.metrics.failed")}
                    value={job.failedDeletes.toLocaleString()}
                  />
                  <Metric
                    label={t("storageMaintenance.metrics.missing")}
                    value={job.missingObjects.toLocaleString()}
                  />
                  <Metric
                    label={t("storageMaintenance.metrics.missingIndexed")}
                    value={job.missingIndexedObjects.toLocaleString()}
                  />
                  <Metric
                    label={t("storageMaintenance.metrics.indexRows")}
                    value={job.uploadedHashRowsDeleted.toLocaleString()}
                  />
                </Box>
              </Stack>
            </>
          )}

          <Box display="flex" justifyContent="flex-end" gap={1}>
            {active && job ? (
              <Tooltip title={t("storageMaintenance.cancel")}>
                <span>
                  <Button
                    color="warning"
                    variant="outlined"
                    startIcon={
                      canceling ? <CircularProgress size={16} /> : <Cancel />
                    }
                    disabled={canceling}
                    onClick={() => onCancel(job.jobId)}
                  >
                    {t("storageMaintenance.cancel")}
                  </Button>
                </span>
              </Tooltip>
            ) : (
              <Tooltip title={t("storageMaintenance.start")}>
                <span>
                  <Button
                    color="primary"
                    variant="contained"
                    startIcon={
                      starting ? (
                        <CircularProgress size={16} />
                      ) : (
                        <CleaningServices />
                      )
                    }
                    disabled={starting}
                    onClick={onStart}
                  >
                    {t("storageMaintenance.start")}
                  </Button>
                </span>
              </Tooltip>
            )}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}

interface MetricProps {
  label: string;
  value: string;
}

function Metric({ label, value }: MetricProps) {
  return (
    <Box minWidth={0}>
      <Typography variant="caption" color="text.secondary" noWrap>
        {label}
      </Typography>
      <Typography variant="body2" fontWeight={600} noWrap>
        {value}
      </Typography>
    </Box>
  );
}
