import {
  ArrowRightAlt,
  Folder,
  GitHub,
  Web,
  YouTube,
} from "@mui/icons-material";
import {
  Box,
  MenuItem,
  Paper,
  Select,
  TextField,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";

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
      name: "SSH",
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
          <Box>
            <Typography variant="body1">{t("createBackup.source")}</Typography>
            <Select label={t("createBackup.source")} title="Select a source">
              {sources.map((source) => (
                <MenuItem
                  key={source.id}
                  value={source.id}
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 1,
                  }}
                >
                  {source.icon}
                  {source.name}
                </MenuItem>
              ))}
            </Select>
          </Box>
          <Box>
            <ArrowRightAlt fontSize="large" />
          </Box>
          <Box>
            <Typography variant="body1">
              {t("createBackup.destination")}
            </Typography>
            <Select label={t("createBackup.source")} title="Select a source">
              {destinations.map((source) => (
                <MenuItem
                  key={source.id}
                  value={source.id}
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 1,
                  }}
                >
                  {source.icon}
                  {source.name}
                </MenuItem>
              ))}
            </Select>
          </Box>
        </Box>
      </Box>
    </Paper>
  );
};

export default CreateBackup;
