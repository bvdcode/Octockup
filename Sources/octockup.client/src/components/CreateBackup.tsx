import { toast } from "react-toastify";
import { useTranslation } from "react-i18next";
import { DirectionArrow, PeriodSelector, ProviderSelector } from ".";
import { Cloud, Folder, GitHub, Web, YouTube } from "@mui/icons-material";
import { Box, Button, Paper, TextField, Typography } from "@mui/material";

const CreateBackup: React.FC = () => {
  const { t } = useTranslation();

  const sources = [
    {
      id: 1,
      name: "GitHub",
      icon: <GitHub />,
    },
    {
      id: 2,
      name: "YouTube",
      icon: <YouTube />,
    },
    {
      id: 3,
      name: "SCP",
      icon: <Web />,
    },
    {
      id: 4,
      name: "Local",
      icon: <Folder />,
    },
    {
      id: 5,
      name: "OneDrive",
      icon: <Cloud />,
    },
  ];

  const destinations = [
    {
      id: 1,
      name: "Local",
      icon: <Folder />,
    },
    {
      id: 2,
      name: "OneDrive",
      icon: <Cloud />,
    },
    {
      id: 3,
      name: "Nextcloud",
      icon: <Cloud />,
    },
  ];

  return (
    <Paper
      sx={{
        display: "flex",
        flexDirection: "column",
        width: "100%",
        height: "100%",
        alignItems: "center",
        borderRadius: 0,
        padding: 2,
        gap: 2,
      }}
    >
      <Typography variant="h6">{t("createBackup.title")}</Typography>
      <Box
        display="flex"
        flexDirection="column"
        width="100%"
        maxWidth="600px"
        justifyContent="space-around"
        gap={1}
      >
        <TextField label={t("createBackup.name")} />
        <Box
          display="flex"
          flexDirection={{
            xs: "column",
            md: "row",
          }}
          alignItems="center"
          justifyContent="space-around"
          gap={1}
        >
          <ProviderSelector
            title={t("createBackup.source")}
            values={sources}
            onSelected={(value) => toast.info(value.name)}
          />
          <DirectionArrow />
          <ProviderSelector
            title={t("createBackup.destination")}
            values={destinations}
            onSelected={(value) => toast.info(value.name)}
          />
        </Box>
        <PeriodSelector onSelectPeriod={(result) => toast.info(result)} />
        <Button variant="contained" fullWidth color="primary">
          {t("createBackup.create")}
        </Button>
      </Box>
    </Paper>
  );
};

export default CreateBackup;
