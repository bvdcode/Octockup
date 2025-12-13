import { Box, CircularProgress, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";

export function EnumerationProgress() {
  const { t } = useTranslation();

  return (
    <Box display="flex" alignItems="center" gap={0.5} mb={0.5}>
      <CircularProgress
        size={12}
        sx={{
          animation: "pulse 1.5s ease-in-out infinite",
          "@keyframes pulse": {
            "0%, 100%": { opacity: 1 },
            "50%": { opacity: 0.4 },
          },
        }}
      />
      <Typography
        variant="caption"
        color="text.secondary"
        sx={{
          animation: "pulse 1.5s ease-in-out infinite",
          "@keyframes pulse": {
            "0%, 100%": { opacity: 1 },
            "50%": { opacity: 0.4 },
          },
        }}
      >
        {t("backups.enumerating")}
      </Typography>
    </Box>
  );
}
