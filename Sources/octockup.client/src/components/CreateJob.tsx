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
} from "@mui/material";
import { getProviders } from "../api/api";
import { useEffect, useState } from "react";
import { BackupProvider } from "../api/types";
import { useTranslation } from "react-i18next";
import IntervalInput from "./IntervalInput";

const CreateJob: React.FC = () => {
  const { t } = useTranslation();
  const [providers, setProviders] = useState<BackupProvider[]>([]);
  const [selectedProvider, setSelectedProvider] =
    useState<BackupProvider | null>();
  const [interval, setInterval] = useState<number>(0);

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
            [{interval}s]
            <IntervalInput
              label={t("createJob.interval")}
              defaultValue={600}
              onChange={(seconds) => setInterval(seconds)}
            />
            <FormControl fullWidth margin="normal">
              <InputLabel id="notifications-select-label">
                {t("notifications")}
              </InputLabel>
              <Select
                labelId="notifications-select-label"
                id="notifications-select"
                label={t("notifications")}
              >
                <MenuItem value="yes">{t("yes")}</MenuItem>
                <MenuItem value="no">{t("no")}</MenuItem>
              </Select>
            </FormControl>
          </CardContent>
        </Card>
      </Stack>
    </Box>
  );
};

export default CreateJob;
