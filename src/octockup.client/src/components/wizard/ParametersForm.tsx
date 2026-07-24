import {
  Card,
  Button,
  Stack,
  Checkbox,
  TextField,
  Typography,
  IconButton,
  CardContent,
  InputAdornment,
  FormControlLabel,
} from "@mui/material";
import { useState } from "react";
import type { ClipboardEvent } from "react";
import { useTranslation } from "react-i18next";
import type { ModuleProviderInfo } from "../../types/api";
import { Visibility, VisibilityOff } from "@mui/icons-material";
import { CHECKBOX_PARAMETERS } from "../../constants/checkboxParameters";
import {
  isEncryptedPuttyKey,
  PuttyKeyError,
  unlockPuttyKey,
} from "../../utils/puttyPrivateKey";

const SFTP_PROVIDER_ID = "Octockup.Server.Modules.SFTPBackupStorage";

interface ParamState {
  [key: string]: string;
}

interface ParametersFormProps {
  moduleMeta: ModuleProviderInfo;
  params: ParamState;
  tag: string;
  onParamChange: (name: string, value: string) => void;
  onTagChange: (value: string) => void;
  onParamsPaste: (e: ClipboardEvent<HTMLInputElement>) => void;
  disabled?: boolean;
}

export function ParametersForm({
  moduleMeta,
  params,
  tag,
  onParamChange,
  onTagChange,
  onParamsPaste,
  disabled,
}: ParametersFormProps) {
  const { t } = useTranslation();
  const [showPassword, setShowPassword] = useState<Record<string, boolean>>({});
  const [keyPassphrase, setKeyPassphrase] = useState("");
  const [keyError, setKeyError] = useState<string | null>(null);
  const [unlockingKey, setUnlockingKey] = useState(false);

  const togglePasswordVisibility = (paramName: string) => {
    setShowPassword((prev) => ({
      ...prev,
      [paramName]: !prev[paramName],
    }));
  };

  const unlockPrivateKey = async () => {
    try {
      setUnlockingKey(true);
      setKeyError(null);
      const unlocked = await unlockPuttyKey(
        params.password ?? "",
        keyPassphrase,
      );
      onParamChange("password", unlocked);
      setKeyPassphrase("");
    } catch (error: unknown) {
      const key =
        error instanceof PuttyKeyError ? error.code : "sftpKeyInvalid";
      setKeyError(t(`wizard.${key}`));
    } finally {
      setUnlockingKey(false);
    }
  };

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>
          {t("wizard.parameters")}
        </Typography>
        <Stack spacing={2}>
          <TextField
            required
            fullWidth
            label={t("wizard.tag")}
            value={tag}
            onChange={(e) => onTagChange(e.target.value)}
            placeholder={t("wizard.enterTag")}
            disabled={disabled}
          />
          {moduleMeta.requiredParameters.length === 0 ? (
            <Typography
              variant="body2"
              color="text.secondary"
              fontStyle="italic"
            >
              {t("wizard.noParameters")}
            </Typography>
          ) : (
            <>
              {moduleMeta.requiredParameters.map((p) => {
                const isCheckbox = CHECKBOX_PARAMETERS.includes(p);

                if (isCheckbox) {
                  return (
                    <FormControlLabel
                      key={p}
                      control={
                        <Checkbox
                          checked={params[p] === "true"}
                          onChange={(e) =>
                            onParamChange(
                              p,
                              e.target.checked ? "true" : "false",
                            )
                          }
                          disabled={disabled}
                        />
                      }
                      label={p}
                    />
                  );
                }

                const isSftpCredential =
                  moduleMeta.id === SFTP_PROVIDER_ID && p === "password";

                if (isSftpCredential) {
                  const encrypted = isEncryptedPuttyKey(params[p] ?? "");
                  return (
                    <Stack key={p} spacing={2}>
                      <TextField
                        required
                        fullWidth
                        label={t("wizard.sftpCredential")}
                        multiline
                        minRows={3}
                        maxRows={10}
                        value={params[p] ?? ""}
                        onChange={(event) => {
                          setKeyPassphrase("");
                          setKeyError(null);
                          onParamChange(p, event.target.value);
                        }}
                        placeholder={t("wizard.sftpCredentialPlaceholder")}
                        helperText={t("wizard.sftpCredentialHelp")}
                        disabled={disabled || unlockingKey}
                      />
                      {encrypted && (
                        <Stack
                          direction={{ xs: "column", sm: "row" }}
                          spacing={1}
                          alignItems={{ sm: "flex-start" }}
                        >
                          <TextField
                            required
                            fullWidth
                            label={t("wizard.sftpKeyPassphrase")}
                            type="password"
                            value={keyPassphrase}
                            onChange={(event) => {
                              setKeyPassphrase(event.target.value);
                              setKeyError(null);
                            }}
                            error={Boolean(keyError)}
                            helperText={
                              keyError ?? t("wizard.sftpKeyPassphraseHelp")
                            }
                            autoComplete="new-password"
                            disabled={disabled || unlockingKey}
                          />
                          <Button
                            type="button"
                            variant="outlined"
                            onClick={unlockPrivateKey}
                            disabled={
                              disabled || unlockingKey || !keyPassphrase
                            }
                            sx={{ minWidth: 150, minHeight: 56 }}
                          >
                            {unlockingKey
                              ? t("wizard.sftpKeyUnlocking")
                              : t("wizard.sftpKeyUnlock")}
                          </Button>
                        </Stack>
                      )}
                    </Stack>
                  );
                }

                const isPassword = p.toLowerCase().includes("password");
                const inputType =
                  isPassword && !showPassword[p] ? "password" : "text";

                return (
                  <TextField
                    key={p}
                    required={p !== "path"}
                    fullWidth
                    label={p}
                    type={inputType}
                    value={params[p] ?? ""}
                    onChange={(e) => onParamChange(p, e.target.value)}
                    onPaste={onParamsPaste}
                    placeholder={t("wizard.enterValue", { param: p })}
                    disabled={disabled}
                    InputProps={
                      isPassword
                        ? {
                            endAdornment: (
                              <InputAdornment position="end">
                                <IconButton
                                  onClick={() => togglePasswordVisibility(p)}
                                  edge="end"
                                  disabled={disabled}
                                >
                                  {showPassword[p] ? (
                                    <VisibilityOff />
                                  ) : (
                                    <Visibility />
                                  )}
                                </IconButton>
                              </InputAdornment>
                            ),
                          }
                        : undefined
                    }
                  />
                );
              })}
            </>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}
