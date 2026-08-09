import { CleaningServices, OpenInNew } from "@mui/icons-material";
import { Button, Card, CardContent, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";

export default function StorageCleanupCard() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  return (
    <Card>
      <CardContent>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          alignItems={{ xs: "stretch", sm: "center" }}
          justifyContent="space-between"
          spacing={2}
        >
          <Stack direction="row" alignItems="center" spacing={2}>
            <CleaningServices color="primary" />
            <Stack>
              <Typography variant="h6">{t("settings.cleanup.title")}</Typography>
              <Typography variant="body2" color="text.secondary">
                {t("settings.cleanup.description")}
              </Typography>
            </Stack>
          </Stack>
          <Button
            variant="outlined"
            endIcon={<OpenInNew />}
            onClick={() => navigate("/admin/storage-cleanup")}
          >
            {t("settings.cleanup.openDashboard")}
          </Button>
        </Stack>
      </CardContent>
    </Card>
  );
}
