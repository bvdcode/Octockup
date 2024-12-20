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
} from "@mui/material";
import { getProviders } from "../api/api";
import IntervalInput from "./IntervalInput";
import { useEffect, useState } from "react";
import { BackupProvider } from "../api/types";
import { useTranslation } from "react-i18next";

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
    useState<BackupProvider | null>();
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

  return (
    <Box sx={{ height: "100%", width: "100%", overflow: "auto", padding: 5 }}>
      <Stack spacing={2}>
        <Typography variant="h4">{t("createJob.title")}</Typography>
        <Card>
          <CardContent>
            <Typography variant="h6">
              {t("createJob.selectProvider")}
            </Typography>
            <FormControl fullWidth margin="normal">
              <InputLabel id="provider-select-label">
                {t("createJob.provider")}
              </InputLabel>
              <Select
                labelId="provider-select-label"
                id="provider-select"
                label={t("createJob.provider")}
                value={selectedProvider?.name}
                onChange={(event) => {
                  const provider = providers.find(
                    (p) => p.name === event.target.value
                  );
                  setSelectedProvider(provider || null);
                }}
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
            />
            <IntervalInput
              label={t("createJob.interval") + ` (${request.interval} s)`}
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
            <pre>{JSON.stringify(request, null, 2)}</pre>
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
