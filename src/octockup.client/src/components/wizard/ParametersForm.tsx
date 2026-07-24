import {
  Card,
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

  const togglePasswordVisibility = (paramName: string) => {
    setShowPassword((prev) => ({
      ...prev,
      [paramName]: !prev[paramName],
    }));
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

                const isPassword = p.toLowerCase().includes("password");
                const isSftpCredential =
                  moduleMeta.id === SFTP_PROVIDER_ID && p === "password";
                const inputType =
                  isPassword && !isSftpCredential && !showPassword[p]
                    ? "password"
                    : "text";

                return (
                  <TextField
                    key={p}
                    required={p !== "path"}
                    fullWidth
                    label={
                      isSftpCredential ? t("wizard.sftpCredential") : p
                    }
                    type={inputType}
                    multiline={isSftpCredential}
                    minRows={isSftpCredential ? 3 : undefined}
                    maxRows={isSftpCredential ? 10 : undefined}
                    value={params[p] ?? ""}
                    onChange={(e) => onParamChange(p, e.target.value)}
                    onPaste={isSftpCredential ? undefined : onParamsPaste}
                    placeholder={
                      isSftpCredential
                        ? t("wizard.sftpCredentialPlaceholder")
                        : t("wizard.enterValue", { param: p })
                    }
                    helperText={
                      isSftpCredential
                        ? t("wizard.sftpCredentialHelp")
                        : undefined
                    }
                    disabled={disabled}
                    sx={
                      isSftpCredential && !showPassword[p]
                        ? {
                            "& textarea": {
                              WebkitTextSecurity: "disc",
                            },
                          }
                        : undefined
                    }
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
