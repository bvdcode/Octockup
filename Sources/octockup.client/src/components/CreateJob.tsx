import {
  Box,
  Card,
  CardContent,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  SelectChangeEvent,
  Stack,
  TextField,
  Typography,
  Button,
} from "@mui/material";
import { getProviders } from "../api/api";
import IntervalInput from "./IntervalInput";
import { BackupProvider } from "../api/types";
import { useTranslation } from "react-i18next";
import { initialState, reducer } from "./CreateJobReducer";
import React, { useEffect, useReducer, useState } from "react";
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import {
  materialDark,
  materialLight,
} from "react-syntax-highlighter/dist/esm/styles/prism";

const CreateJob: React.FC = () => {
  const { t } = useTranslation();
  const [providers, setProviders] = useState<BackupProvider[]>([]);
  const [selectedProvider, setSelectedProvider] =
    useState<BackupProvider | null>(null);
  const [state, dispatch] = useReducer(reducer, initialState);

  useEffect(() => {
    getProviders().then((response) => {
      setProviders(response);
    });
  }, []);

  const handleProviderChange = (event: SelectChangeEvent<string>) => {
    const value = event.target.value;
    const provider = providers.find((p) => p.name === value);
    setSelectedProvider(provider || null);
    dispatch({ type: "SET_PROVIDER", payload: value });
  };

  const getCodeStyle = () => {
    if (window.matchMedia("(prefers-color-scheme: dark)").matches) {
      return materialDark;
    }
    return materialLight;
  };

  return (
    <Box sx={{ height: "100%", width: "100%", overflow: "auto", padding: 5 }}>
      <Stack spacing={2}>
        <Typography variant="h4">{t("createJob.title")}</Typography>
        <Card>
          <CardContent>
            <FormControl fullWidth>
              <InputLabel>{t("createJob.provider")}</InputLabel>
              <Select
                value={state.provider}
                label={t("createJob.provider")}
                onChange={handleProviderChange}
              >
                <MenuItem value="">{t("createJob.notSelected")}</MenuItem>
                {providers.map((provider) => (
                  <MenuItem key={provider.name} value={provider.name}>
                    {provider.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            {selectedProvider ? (
              selectedProvider.parameters.map((parameter) => (
                <TextField
                  key={parameter}
                  fullWidth
                  margin="normal"
                  label={parameter}
                  variant="outlined"
                  onChange={(event) =>
                    dispatch({
                      type: "SET_SETTINGS",
                      payload: { key: parameter, value: event.target.value },
                    })
                  }
                />
              ))
            ) : (
              <Typography sx={{ fontStyle: "italic", color: "text.secondary" }}>
                {t("createJob.selectProviderFirst")}
              </Typography>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <IntervalInput
              label={t("createJob.interval")}
              onChange={(interval) =>
                dispatch({ type: "SET_INTERVAL", payload: interval })
              }
              defaultValue={0}
            />
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <FormControl fullWidth>
              <InputLabel>{t("notifications")}</InputLabel>
              <Select
                value={state.notifications ? "yes" : "no"}
                label={t("notifications")}
                onChange={(event) =>
                  dispatch({
                    type: "SET_NOTIFICATIONS",
                    payload: event.target.value === "yes",
                  })
                }
              >
                <MenuItem value="yes">{t("yes")}</MenuItem>
                <MenuItem value="no">{t("no")}</MenuItem>
              </Select>
            </FormControl>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <FormControl fullWidth>
              <TextField
                type="datetime-local"
                fullWidth
                label={t("createJob.startAt")}
                margin="normal"
                variant="outlined"
                defaultValue={null}
                slotProps={{ inputLabel: { shrink: true } }}
                onChange={(event) =>
                  dispatch({
                    type: "SET_START_AT",
                    payload: new Date(event.target.value),
                  })
                }
              />
            </FormControl>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="h6">{t("createJob.preview")}</Typography>
            <SyntaxHighlighter
              language="json"
              style={getCodeStyle()}
              customStyle={{
                borderRadius: 15,
                boxShadow: "inset 0 0 5px rgba(0, 0, 0, 0.18)",
              }}
            >
              {JSON.stringify(state, null, 2)}
            </SyntaxHighlighter>
          </CardContent>
        </Card>

        <Card>
          <CardContent
            sx={{
              display: "flex",
              justifyContent: "center",
            }}
          >
            <Button fullWidth={true}>{t("createJob.submitButton")}</Button>
          </CardContent>
        </Card>
      </Stack>
    </Box>
  );
};

export default CreateJob;
