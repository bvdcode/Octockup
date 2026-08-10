import {
  AccessTime,
  EventBusy,
  MoreTime,
  PlayArrow,
} from "@mui/icons-material";
import {
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  Popover,
  Stack,
  TextField,
  Tooltip,
} from "@mui/material";
import { useState, type MouseEvent } from "react";
import { useTranslation } from "react-i18next";

const QUICK_INTERVALS = [
  { minutes: 60, translationKey: "backups.schedule.quick.hour" },
  { minutes: 1_440, translationKey: "backups.schedule.quick.day" },
  { minutes: 10_080, translationKey: "backups.schedule.quick.week" },
  { minutes: 43_200, translationKey: "backups.schedule.quick.month" },
] as const;

interface BackupRunMenuProps {
  disabled: boolean;
  intervalMinutes: number | null;
  loading: boolean;
  onDisableSchedule: () => Promise<void>;
  onRunNow: () => Promise<void>;
  onSetSchedule: (intervalMinutes: number) => Promise<void>;
}

export function BackupRunMenu({
  disabled,
  intervalMinutes,
  loading,
  onDisableSchedule,
  onRunNow,
  onSetSchedule,
}: BackupRunMenuProps) {
  const { t } = useTranslation();
  const [anchor, setAnchor] = useState<HTMLButtonElement | null>(null);
  const [customOpen, setCustomOpen] = useState(false);
  const [customInterval, setCustomInterval] = useState("");
  const parsedCustomInterval = Number(customInterval.trim());
  const customIntervalValid =
    Number.isInteger(parsedCustomInterval) && parsedCustomInterval > 0;

  const close = () => setAnchor(null);

  const runAndClose = async (action: () => Promise<void>) => {
    await action();
    close();
  };

  const openCustom = () => {
    setCustomInterval(intervalMinutes?.toString() ?? "");
    setCustomOpen(true);
    close();
  };

  const saveCustom = async () => {
    if (!customIntervalValid) {
      return;
    }
    await onSetSchedule(parsedCustomInterval);
    setCustomOpen(false);
  };

  return (
    <>
      <Tooltip title={t("backups.runOrSchedule")} placement="left">
        <span>
          <IconButton
            size="small"
            color={intervalMinutes ? "warning" : "success"}
            aria-label={t("backups.runOrSchedule")}
            disabled={disabled || loading}
            onClick={(event: MouseEvent<HTMLButtonElement>) =>
              setAnchor(event.currentTarget)
            }
          >
            {loading ? (
              <CircularProgress size={20} color="inherit" />
            ) : intervalMinutes ? (
              <AccessTime />
            ) : (
              <PlayArrow />
            )}
          </IconButton>
        </span>
      </Tooltip>
      <Popover
        open={Boolean(anchor)}
        anchorEl={anchor}
        onClose={close}
        anchorOrigin={{ vertical: "center", horizontal: "left" }}
        transformOrigin={{ vertical: "center", horizontal: "right" }}
        slotProps={{
          paper: { sx: { maxWidth: "calc(100% - 16px)" } },
        }}
      >
        <Stack
          direction="row"
          alignItems="center"
          flexWrap="wrap"
          gap={0.5}
          p={0.75}
        >
          <Button
            size="small"
            color="inherit"
            startIcon={<PlayArrow />}
            onClick={() => void runAndClose(onRunNow)}
          >
            {t("backups.schedule.now")}
          </Button>
          <Divider
            orientation="vertical"
            flexItem
            sx={{ display: { xs: "none", sm: "block" } }}
          />
          {QUICK_INTERVALS.map((interval) => (
            <Button
              key={interval.minutes}
              size="small"
              color={
                intervalMinutes === interval.minutes ? "primary" : "inherit"
              }
              variant={
                intervalMinutes === interval.minutes ? "contained" : "text"
              }
              onClick={() =>
                void runAndClose(() => onSetSchedule(interval.minutes))
              }
            >
              {t(interval.translationKey)}
            </Button>
          ))}
          <Tooltip title={t("backups.schedule.custom")}>
            <IconButton
              size="small"
              aria-label={t("backups.schedule.custom")}
              onClick={openCustom}
            >
              <MoreTime />
            </IconButton>
          </Tooltip>
          {intervalMinutes !== null && (
            <Tooltip title={t("backups.schedule.off")}>
              <IconButton
                size="small"
                color="error"
                aria-label={t("backups.schedule.off")}
                onClick={() => void runAndClose(onDisableSchedule)}
              >
                <EventBusy />
              </IconButton>
            </Tooltip>
          )}
        </Stack>
      </Popover>
      <Dialog open={customOpen} onClose={() => setCustomOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>{t("backups.schedule.customTitle")}</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            type="number"
            label={t("backups.schedule.intervalMinutes")}
            value={customInterval}
            error={customInterval.length > 0 && !customIntervalValid}
            helperText={t("backups.schedule.customHint")}
            onChange={(event) => setCustomInterval(event.target.value)}
            slotProps={{ htmlInput: { min: 1, step: 1 } }}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCustomOpen(false)}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="contained"
            disabled={!customIntervalValid || loading}
            onClick={() => void saveCustom()}
          >
            {t("common.save")}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
