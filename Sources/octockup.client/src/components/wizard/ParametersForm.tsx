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
import { Visibility, VisibilityOff } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { ClipboardEvent } from "react";
import { useState } from "react";
import type { ModuleProviderInfo } from "../../types/api";

const CHECKBOX_PARAMETERS = ["skipPermissionDenied"];

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
