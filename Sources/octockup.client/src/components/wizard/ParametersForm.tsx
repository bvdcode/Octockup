import { Card, CardContent, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { ClipboardEvent } from "react";
import type { ModuleProviderInfo } from "../../types/api";

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
            <Typography variant="body2" color="text.secondary" fontStyle="italic">
              {t("wizard.noParameters")}
            </Typography>
          ) : (
            <>
              {moduleMeta.requiredParameters.map((p) => (
                <TextField
                  key={p}
                  required={p !== "path"}
                  fullWidth
                  label={p}
                  value={params[p] ?? ""}
                  onChange={(e) => onParamChange(p, e.target.value)}
                  onPaste={onParamsPaste}
                  placeholder={t("wizard.enterValue", { param: p })}
                  disabled={disabled}
                />
              ))}
            </>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}
