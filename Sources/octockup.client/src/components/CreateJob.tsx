import {
  Box,
  Card,
  Stack,
  Select,
  MenuItem,
  TextField,
  InputLabel,
  Typography,
  FormControl,
  CardContent,
  Button,
  SelectChangeEvent,
} from "@mui/material";
import { getProviders } from "../api/api";
import IntervalInput from "./IntervalInput";
import { useEffect, useState } from "react";
import { BackupProvider } from "../api/types";
import { useTranslation } from "react-i18next";
import { Prism as SyntaxHighlighter } from "react-syntax-highlighter";
import { dark } from "react-syntax-highlighter/dist/esm/styles/prism";

export interface CreateJobRequest {
  provider: string;
  settings: Record<string, string>;
  jobName: string;
  interval: number;
  notifications: boolean;
}

const CreateJob: React.FC = () => {
  const { t } = useTranslation();
  const [providers, setProviders] = useState<BackupProvider[]>([]);
  const [selectedProvider, setSelectedProvider] =
    useState<BackupProvider | null>(null);
  const [request, setRequest] = useState<CreateJobRequest>({
    provider: "",
    settings: {},
    jobName: "",
    interval: 0,
    notifications: false,
  });

  useEffect(() => {
    getProviders().then((response) => {
      setProviders(response);
    });
  }, []);

  const handleProviderChange = (event: SelectChangeEvent<string>) => {
    const value = event.target.value;
    const provider = providers.find((p) => p.name === value);
    setSelectedProvider(provider || null);
    setRequest((prev) => ({
      ...prev,
      provider: value,
      settings: {},
    }));
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
                value={request.provider}
                label={t("createJob.provider")}
                onChange={handleProviderChange}
              >
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
              selectedProvider?.parameters.map((parameter) => (
                <TextField
                  key={parameter}
                  fullWidth
                  margin="normal"
                  label={parameter}
                  variant="outlined"
                  onChange={(event) =>
                    setRequest((prev) => ({
                      ...prev,
                      settings: {
                        ...prev.settings,
                        [parameter]: event.target.value,
                      },
                    }))
                  }
                />
              ))
            ) : (
              <Typography
                variant="body2"
                color="textSecondary"
                sx={{ fontStyle: "italic" }}
              >
                {t("createJob.selectProviderFirst")}
              </Typography>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="h6">{t("createJob.jobSettings")}</Typography>
            <TextField
              fullWidth
              margin="normal"
              label={t("createJob.jobName")}
              variant="outlined"
              onChange={(event) =>
                setRequest((prev) => ({
                  ...prev,
                  jobName: event.target.value,
                }))
              }
            />
            <IntervalInput
              label={t("createJob.interval")}
              defaultValue={0}
              onChange={(seconds) =>
                setRequest((prev) => ({ ...prev, interval: seconds }))
              }
            />
            <FormControl fullWidth margin="normal">
              <InputLabel id="notifications-select-label">
                {t("notifications")}
              </InputLabel>
              <Select
                labelId="notifications-select-label"
                id="notifications-select"
                label={t("notifications")}
                value={request.notifications ? "yes" : "no"}
                onChange={(event) => {
                  setRequest((prev) => ({
                    ...prev,
                    notifications: event.target.value === "yes",
                  }));
                }}
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
              {JSON.stringify(request, null, 2)}
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
