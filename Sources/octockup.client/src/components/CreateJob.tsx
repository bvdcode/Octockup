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
  Tooltip,
  IconButton,
} from "@mui/material";
import { AxiosError } from "axios";
import { toast } from "react-toastify";
import { Info } from "@mui/icons-material";
import IntervalInput from "./IntervalInput";
import { BackupProvider } from "../api/types";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import {
  materialDark,
  materialLight,
} from "react-syntax-highlighter/dist/esm/styles/prism";
import { createBackupJob, getProviders } from "../api/api";
import { initialState, reducer } from "./CreateJobReducer";
import React, { useEffect, useReducer, useState } from "react";
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";

const CreateJob: React.FC = () => {
  const { t } = useTranslation();
  const [providers, setProviders] = useState<BackupProvider[]>([]);
  const [selectedProvider, setSelectedProvider] =
    useState<BackupProvider | null>(null);
  const [state, dispatch] = useReducer(reducer, initialState);
  const navigate = useNavigate();

  useEffect(() => {
    getProviders().then((response) => {
      setProviders(response);
    });
  }, []);

  const handleProviderChange = (event: SelectChangeEvent<string>) => {
    const value = event.target.value;
    const provider = providers.find((p) => p.class === value);
    setSelectedProvider(provider || null);
    dispatch({ type: "SET_PROVIDER", payload: value });
    if (provider) {
      provider.parameters.forEach((parameter) => {
        dispatch({
          type: "SET_SETTINGS",
          payload: { key: parameter, value: "" },
        });
      });
    }
  };

  const getCodeStyle = () => {
    if (window.matchMedia("(prefers-color-scheme: dark)").matches) {
      return materialDark;
    }
    return materialLight;
  };

  const formatParameter = (parameter: string) => {
    if (parameter.match(/[A-Z]/)) {
      return parameter
        .split(/(?=[A-Z])/)
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
        .join(" ");
    }
  };

  const handleCreateJobClick = () => {
    if (!state.provider) {
      toast.warning(t("createJob.providerNotSelected"));
      return;
    }
    if (!state.name || state.name.trim() === "") {
      toast.warning(t("createJob.jobNameNotProvided"));
      return;
    }

    createBackupJob(state)
      .then(() => {
        toast.success(t("createJob.success"));
        navigate("/dashboard");
      })
      .catch((error: AxiosError) => {
        if (error.response?.status === 400) {
          const errors = (
            error.response?.data as { errors: Record<string, string[]> }
          ).errors;
          const errorMessages = Object.keys(errors).map((key) => {
            return `${errors[key].join(", ")}`;
          });
          toast.error(
            t("createJob.error", { error: errorMessages.join(", ") })
          );
          return;
        }
        toast.error(t("createJob.error", { error: error.message }));
      });
  };

  return (
    <Box
      sx={{
        height: "100%",
        width: "100%",
        overflow: "auto",
        padding: { xs: 2, md: 5 },
        paddingBottom: { xs: 5 },
      }}
    >
      <Stack spacing={2}>
        <Box display="flex" alignItems="center" justifyContent="space-between">
          <Typography variant="h5">{t("createJob.title")}</Typography>
          <Tooltip title={t("createJob.help")}>
            <IconButton size="small">
              <Info fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>
        <Card>
          <CardContent>
            <FormControl fullWidth>
              <InputLabel>{t("createJob.provider")}</InputLabel>
              <Select
                required
                value={state.provider}
                label={t("createJob.provider")}
                onChange={handleProviderChange}
              >
                <MenuItem value="">{t("createJob.notSelected")}</MenuItem>
                {providers.map((provider) => (
                  <MenuItem key={provider.name} value={provider.class}>
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
                  label={formatParameter(parameter)}
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
            <TextField
              fullWidth
              margin="normal"
              label={t("createJob.jobName")}
              variant="outlined"
              onChange={(event) =>
                dispatch({ type: "SET_JOB_NAME", payload: event.target.value })
              }
            />
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <FormControl fullWidth>
              <InputLabel>{t("notifications")}</InputLabel>
              <Select
                value={state.isNotificationEnabled ? "yes" : "no"}
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
              <TextField
                sx={{
                  "& ::-webkit-calendar-picker-indicator": {
                    filter: (theme) =>
                      theme.palette.mode === "dark" ? "invert(1)" : "none",
                  },
                }}
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
            <Button fullWidth={true} onClick={handleCreateJobClick}>
              {t("createJob.submitButton")}
            </Button>
          </CardContent>
        </Card>
      </Stack>
    </Box>
  );
};

export default CreateJob;
