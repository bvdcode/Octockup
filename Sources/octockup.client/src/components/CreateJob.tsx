import { useEffect, useState } from "react";
import { BackupProvider } from "../api/types";
import { getProviders } from "../api/api";
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
import { useTranslation } from "react-i18next";

const CreateJob: React.FC = () => {
  const { t } = useTranslation();
  const [providers, setProviders] = useState<BackupProvider[]>([]);
  const [selectedProvider, setSelectedProvider] =
    useState<BackupProvider | null>();

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
              label="Частота бэкапов"
              variant="outlined"
            />
            <TextField
              fullWidth
              margin="normal"
              label="Когда начинать"
              variant="outlined"
            />
            <FormControl fullWidth margin="normal">
              <InputLabel id="notifications-select-label">
                Уведомления
              </InputLabel>
              <Select
                labelId="notifications-select-label"
                id="notifications-select"
                label="Уведомления"
              >
                <MenuItem value="yes">Да</MenuItem>
                <MenuItem value="no">Нет</MenuItem>
              </Select>
            </FormControl>
          </CardContent>
        </Card>
        <Card>
          <CardContent>
            <Typography variant="h6">Настройки</Typography>
            <TextField
              fullWidth
              margin="normal"
              label="Частота бэкапов"
              variant="outlined"
            />
            <TextField
              fullWidth
              margin="normal"
              label="Когда начинать"
              variant="outlined"
            />
            <FormControl fullWidth margin="normal">
              <InputLabel id="notifications-select-label">
                Уведомления
              </InputLabel>
              <Select
                labelId="notifications-select-label"
                id="notifications-select"
                label="Уведомления"
              >
                <MenuItem value="yes">Да</MenuItem>
                <MenuItem value="no">Нет</MenuItem>
              </Select>
            </FormControl>
          </CardContent>
        </Card>
        <Card>
          <CardContent>
            <Typography variant="h6">Настройки</Typography>
            <TextField
              fullWidth
              margin="normal"
              label="Частота бэкапов"
              variant="outlined"
            />
            <TextField
              fullWidth
              margin="normal"
              label="Когда начинать"
              variant="outlined"
            />
            <FormControl fullWidth margin="normal">
              <InputLabel id="notifications-select-label">
                Уведомления
              </InputLabel>
              <Select
                labelId="notifications-select-label"
                id="notifications-select"
                label="Уведомления"
              >
                <MenuItem value="yes">Да</MenuItem>
                <MenuItem value="no">Нет</MenuItem>
              </Select>
            </FormControl>
          </CardContent>
        </Card>
      </Stack>
    </Box>
  );
};

export default CreateJob;
