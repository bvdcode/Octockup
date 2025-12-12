import {
  Box,
  Button,
  Card,
  CardContent,
  Typography,
  Alert,
  Stack,
} from "@mui/material";
import { Download, Upload } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { useAuthStore } from "@bvdcode/react-kit";
import { useBackupsApi } from "../api/backupsApi";
import { useState, useRef } from "react";

export default function SettingsPage() {
  const { t } = useTranslation();
  const accessToken = useAuthStore((s) => s.accessToken);
  const backupsApi = useBackupsApi();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [importing, setImporting] = useState(false);
  const [importMessage, setImportMessage] = useState<{
    type: "success" | "error";
    text: string;
  } | null>(null);

  const handleExportUserData = () => {
    const url = `/api/v1/backups/server?access_token=${accessToken}`;
    window.open(url, "_blank");
  };

  const handleImportClick = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (
    event: React.ChangeEvent<HTMLInputElement>
  ) => {
    const file = event.target.files?.[0];
    if (!file) return;

    setImporting(true);
    setImportMessage(null);

    try {
      const result = await backupsApi.importServerBackup(file, includeFiles);
      setImportMessage({ type: "success", text: result.message });
    } catch (error: unknown) {
      const errorMessage =
        error instanceof Error ? error.message : "Failed to import backup data";
      setImportMessage({ type: "error", text: errorMessage });
    } finally {
      setImporting(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    }
  };

  return (
    <Box sx={{ padding: 2 }}>
      <Typography variant="h4" gutterBottom>
        {t("settings.title")}
      </Typography>
      <Stack spacing={2}>
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

        <Card>
          <CardContent>
            <Typography variant="h6" gutterBottom>
              {t("settings.dataImport.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              {t("settings.dataImport.description")}
            </Typography>
            {importMessage && (
              <Alert severity={importMessage.type} sx={{ mb: 2 }}>
                {importMessage.text}
              </Alert>
            )}
            <input
              ref={fileInputRef}
              type="file"
              accept=".octockup"
              style={{ display: "none" }}
              onChange={handleFileChange}
            />
            <Button
              variant="contained"
              color="secondary"
              startIcon={<Upload />}
              onClick={handleImportClick}
              disabled={importing}
            >
              {importing
                ? t("settings.dataImport.importing")
                : t("settings.dataImport.button")}
            </Button>
          </CardContent>
        </Card>
      </Stack>
    </Box>
  );
}
