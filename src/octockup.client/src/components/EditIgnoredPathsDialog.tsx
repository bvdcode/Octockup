import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { useState, useMemo } from "react";
import { getIgnoredPathsPreset } from "../constants/ignoredPathsPresets";

interface EditIgnoredPathsDialogProps {
  open: boolean;
  backupModuleId: string;
  initialPaths: string[];
  onClose: () => void;
  onSave: (paths: string[]) => Promise<void>;
  loading?: boolean;
}

export function EditIgnoredPathsDialog({
  open,
  backupModuleId,
  initialPaths,
  onClose,
  onSave,
  loading = false,
}: EditIgnoredPathsDialogProps) {
  const { t } = useTranslation();
  const [ignoredPathsInput, setIgnoredPathsInput] = useState<string>(() =>
    initialPaths.join("\n"),
  );
  const [saving, setSaving] = useState(false);

  const preset = useMemo(
    () => getIgnoredPathsPreset(backupModuleId),
    [backupModuleId],
  );

  const handleSave = async () => {
    setSaving(true);
    try {
      const paths = ignoredPathsInput
        .split(/\r?\n/)
        .filter((x) => x.trim() !== "");
      await onSave(paths);
      onClose();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t("backupWizard.ignoredPaths")}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 2 }}>
          <TextField
            label={t("backupWizard.ignoredPaths")}
            value={ignoredPathsInput}
            onChange={(e) => setIgnoredPathsInput(e.target.value)}
            fullWidth
            multiline
            minRows={8}
            placeholder={t("backupWizard.ignoredPathsPlaceholder")}
          />
          {preset.length > 0 && (
            <Button
              size="small"
              variant="outlined"
              onClick={() => setIgnoredPathsInput(preset.join("\n"))}
              disabled={saving || loading}
            >
              {t("backupWizard.applyPreset")}
            </Button>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving || loading}>
          {t("common.cancel")}
        </Button>
        <Button
          onClick={handleSave}
          variant="contained"
          disabled={saving || loading}
        >
          {t("common.save")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
