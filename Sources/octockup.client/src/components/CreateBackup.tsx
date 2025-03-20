import {
  ArrowDownward,
  ArrowForward,
  Cloud,
  Folder,
  GitHub,
  Web,
  YouTube,
} from "@mui/icons-material";
import { Selector } from ".";
import { toast } from "react-toastify";
import { useTranslation } from "react-i18next";
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
        gap={2}
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
          gap={2}
        >
          <Selector
            title={t("createBackup.source")}
            values={sources}
            onSelected={(value) => toast.info(value.name)}
          />
          <Box>
            <ArrowForward
              fontSize="large"
              sx={{
                display: {
                  xs: "none",
                  md: "block",
                },
              }}
            />
            <ArrowDownward
              fontSize="large"
              sx={{
                display: {
                  xs: "block",
                  md: "none",
                },
              }}
            />
          </Box>
          <Selector
            title={t("createBackup.destination")}
            values={destinations}
            onSelected={(value) => toast.info(value.name)}
          />
        </Box>
        <Button variant="contained" fullWidth color="primary">
          {t("createBackup.create")}
        </Button>
      </Box>
    </Paper>
  );
};

export default CreateBackup;
