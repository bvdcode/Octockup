import { Box, Button, Card, CardContent, Typography } from "@mui/material";
import { Download } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useAuthStore } from "@bvdcode/react-kit";

export default function SettingsPage() {
  const { t } = useTranslation();
  const accessToken = useAuthStore((s) => s.accessToken);

  const handleExportUserData = () => {
    const url = `/api/v1/backups/server?access_token=${accessToken}`;
    window.open(url, "_blank");
  };

  return (
    <Box sx={{ padding: 2 }}>
      <Typography variant="h4" gutterBottom>
        {t("settings.title")}
      </Typography>
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            {t("settings.dataExport.title")}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {t("settings.dataExport.description")}
          </Typography>
          <Button
            variant="contained"
            color="primary"
            startIcon={<Download />}
            onClick={handleExportUserData}
          >
            {t("settings.dataExport.button")}
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
}
