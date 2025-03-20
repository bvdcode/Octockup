import {
  ArrowRightAlt,
  Folder,
  GitHub,
  Web,
  YouTube,
} from "@mui/icons-material";
import { Selector } from ".";
import { toast } from "react-toastify";
import { useTranslation } from "react-i18next";
import { Box, Paper, TextField, Typography } from "@mui/material";

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
  ];

  const destinations = [
    {
      id: 1,
      name: "Local",
      icon: <Folder />,
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
      }}
    >
      <Typography variant="h6">{t("createBackup.title")}</Typography>
      <Box
        display="flex"
        flexDirection="column"
        width="100%"
        maxWidth="800px"
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
            <ArrowRightAlt fontSize="large" />
          </Box>
          <Selector
            title={t("createBackup.destination")}
            values={destinations}
            onSelected={(value) => toast.info(value.name)}
          />
        </Box>
      </Box>
    </Paper>
  );
};

export default CreateBackup;
