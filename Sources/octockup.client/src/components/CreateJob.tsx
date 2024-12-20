import React, { useEffect, useReducer, useState } from "react";
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
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import { dark } from "react-syntax-highlighter/dist/esm/styles/prism";

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

  return (
    <Box sx={{ height: "100%", width: "100%", overflow: "auto", padding: 5 }}>
      <Stack spacing={2}>
        <Typography variant="h4">{t("createJob.title")}</Typography>
        <Card>
          <CardContent>
            <Typography variant="h6">
              {t("createJob.selectProvider")}
            </Typography>
            <FormControl fullWidth>
              <InputLabel>{t("createJob.provider")}</InputLabel>
              <Select
                value={state.provider}
                label={t("createJob.provider")}
                onChange={handleProviderChange}
              >
                <MenuItem value="">
                  <em>{t("common.none")}</em>
                </MenuItem>
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
            <Typography variant="h6">
              {t("createJob.providerSettings")}
            </Typography>
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
              <Typography>{t("createJob.noProviderSelected")}</Typography>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="h6">{t("createJob.interval")}</Typography>
            <IntervalInput
              label={t("createJob.interval")}
              onChange={(interval) =>
                dispatch({ type: "SET_INTERVAL", payload: interval })
              }
              defaultValue={state.interval}
            />
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="h6">{t("createJob.notifications")}</Typography>
            <FormControl fullWidth>
              <InputLabel>{t("createJob.notifications")}</InputLabel>
              <Select
                value={state.notifications ? "yes" : "no"}
                label={t("createJob.notifications")}
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
            <Typography variant="h6">{t("createJob.confirmation")}</Typography>
            <SyntaxHighlighter
              language="json"
              style={dark}
              customStyle={{
                backgroundColor: "transparent",
                textShadow: "0",
                border: "none",
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
            <Button>{t("createJob.createJob")}</Button>
          </CardContent>
        </Card>
      </Stack>
    </Box>
  );
};

export default CreateJob;
