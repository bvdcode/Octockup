import {
  Alert,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Stack,
  TextField,
} from "@mui/material";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import type { CreateUserRequest } from "../../types/auth";

interface CreateUserDialogProps {
  open: boolean;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onCreate: (request: CreateUserRequest) => Promise<void>;
}

export default function CreateUserDialog({
  open,
  saving,
  error,
  onClose,
  onCreate,
}: CreateUserDialogProps) {
  const { t } = useTranslation();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [isAdmin, setIsAdmin] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await onCreate({ username: username.trim(), password, isAdmin });
  };

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth>
      <Stack component="form" onSubmit={handleSubmit}>
        <DialogTitle>{t("settings.users.create")}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} pt={1}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label={t("auth.username")}
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              autoComplete="off"
              required
            />
            <TextField
              label={t("auth.password")}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              type="password"
              autoComplete="new-password"
              required
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={isAdmin}
                  onChange={(event) => setIsAdmin(event.target.checked)}
                />
              }
              label={t("settings.users.admin")}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={saving || !username.trim() || !password}
          >
            {saving ? t("common.saving") : t("common.create")}
          </Button>
        </DialogActions>
      </Stack>
    </Dialog>
  );
}
