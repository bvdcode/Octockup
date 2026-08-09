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
import type {
  OidcProvider,
  SaveOidcProviderRequest,
} from "../../types/auth";
import {
  buildOidcCallbackUrl,
  toOidcProviderRequest,
} from "../../utils/authUtils";

interface OidcProviderDialogProps {
  open: boolean;
  provider: OidcProvider | null;
  saving: boolean;
  error: string | null;
  onClose: () => void;
  onSave: (request: SaveOidcProviderRequest) => Promise<void>;
}

export default function OidcProviderDialog({
  open,
  provider,
  saving,
  error,
  onClose,
  onSave,
}: OidcProviderDialogProps) {
  const { t } = useTranslation();
  const [name, setName] = useState(provider?.name ?? "");
  const [slug, setSlug] = useState(provider?.slug ?? "");
  const [issuer, setIssuer] = useState(provider?.issuer ?? "");
  const [clientId, setClientId] = useState(provider?.clientId ?? "");
  const [clientSecret, setClientSecret] = useState("");
  const [scopes, setScopes] = useState(
    provider?.scopes.join(" ") ?? "openid profile email",
  );
  const [isEnabled, setIsEnabled] = useState(provider?.isEnabled ?? true);
  const [clearClientSecret, setClearClientSecret] = useState(false);
  const publicBaseUrl = window.location.origin;

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await onSave(
      toOidcProviderRequest({
        name,
        slug,
        issuer,
        publicBaseUrl,
        clientId,
        clientSecret,
        clearClientSecret,
        scopes,
        isEnabled,
      }),
    );
  };

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth>
      <Stack component="form" onSubmit={handleSubmit}>
        <DialogTitle>
          {provider
            ? t("settings.oidc.editProvider")
            : t("settings.oidc.addProvider")}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} pt={1}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label={t("settings.oidc.name")}
              value={name}
              onChange={(event) => setName(event.target.value)}
              required
            />
            <TextField
              label={t("settings.oidc.slug")}
              value={slug}
              onChange={(event) => setSlug(event.target.value)}
              helperText={t("settings.oidc.slugHint")}
            />
            <TextField
              label={t("settings.oidc.issuer")}
              value={issuer}
              onChange={(event) => setIssuer(event.target.value)}
              required
            />
            <TextField
              label={t("settings.oidc.callbackUrl")}
              value={buildOidcCallbackUrl(publicBaseUrl)}
              slotProps={{ input: { readOnly: true } }}
            />
            <TextField
              label={t("settings.oidc.clientId")}
              value={clientId}
              onChange={(event) => setClientId(event.target.value)}
              required
            />
            <TextField
              label={t("settings.oidc.clientSecret")}
              type="password"
              value={clientSecret}
              disabled={clearClientSecret}
              onChange={(event) => {
                setClientSecret(event.target.value);
                if (event.target.value.trim()) {
                  setClearClientSecret(false);
                }
              }}
              helperText={
                provider?.hasClientSecret
                  ? t("settings.oidc.secretPreserved")
                  : undefined
              }
            />
            {provider?.hasClientSecret && (
              <FormControlLabel
                control={
                  <Checkbox
                    checked={clearClientSecret}
                    onChange={(event) => {
                      setClearClientSecret(event.target.checked);
                      if (event.target.checked) {
                        setClientSecret("");
                      }
                    }}
                  />
                }
                label={t("settings.oidc.clearSecret")}
              />
            )}
            <TextField
              label={t("settings.oidc.scopes")}
              value={scopes}
              onChange={(event) => setScopes(event.target.value)}
              required
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={isEnabled}
                  onChange={(event) => setIsEnabled(event.target.checked)}
                />
              }
              label={t("settings.oidc.enabled")}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={saving}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="contained" disabled={saving}>
            {saving ? t("common.saving") : t("common.save")}
          </Button>
        </DialogActions>
      </Stack>
    </Dialog>
  );
}
