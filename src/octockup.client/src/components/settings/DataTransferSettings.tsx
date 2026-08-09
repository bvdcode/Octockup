import { Download, Upload } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  FormControlLabel,
  Stack,
  Typography,
} from "@mui/material";
import { useRef, useState, type ChangeEvent } from "react";
import { useTranslation } from "react-i18next";
import { useAuthStore } from "@bvdcode/react-kit";
import { useQueryClient } from "@tanstack/react-query";
import { useBackupsApi } from "../../api/backupsApi";
import { getApiErrorMessage } from "../../utils/apiError";

interface ImportMessage {
  type: "success" | "error";
  text: string;
}

export default function DataTransferSettings() {
  const { t } = useTranslation();
  const accessToken = useAuthStore((state) => state.accessToken);
  const backupsApi = useBackupsApi();
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [importing, setImporting] = useState(false);
  const [importMessage, setImportMessage] = useState<ImportMessage | null>(null);
  const [includeFiles, setIncludeFiles] = useState(false);

  const handleExport = () => {
    const url = `/api/v1/backups/server?access_token=${encodeURIComponent(accessToken ?? "")}&includeFiles=${includeFiles}`;
    window.open(url, "_blank", "noopener,noreferrer");
  };

  const handleFileChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    setImporting(true);
    setImportMessage(null);
    try {
      const result = await backupsApi.importServerBackup(file);
      await queryClient.invalidateQueries();
      setImportMessage({ type: "success", text: result.message });
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setImportMessage({
          type: "error",
          text: getApiErrorMessage(caughtError, t("settings.dataImport.failed")),
        });
      }
    } finally {
      setImporting(false);
      event.target.value = "";
    }
  };

  return (
    <>
      <Card>
        <CardContent>
          <Stack spacing={2}>
            <Box>
              <Typography variant="h6">
                {t("settings.dataExport.title")}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t("settings.dataExport.description")}
              </Typography>
            </Box>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
              <Button
                variant="contained"
                startIcon={<Download />}
                onClick={handleExport}
              >
                {t("settings.dataExport.button")}
              </Button>
              <FormControlLabel
                control={
                  <Checkbox
                    checked={includeFiles}
                    onChange={(event) => setIncludeFiles(event.target.checked)}
                  />
                }
                label={t("settings.dataExport.includeFiles")}
              />
            </Stack>
          </Stack>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Stack spacing={2}>
            <Box>
              <Typography variant="h6">
                {t("settings.dataImport.title")}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {t("settings.dataImport.description")}
              </Typography>
            </Box>
            {importMessage && (
              <Alert severity={importMessage.type}>{importMessage.text}</Alert>
            )}
            <input
              ref={fileInputRef}
              type="file"
              accept=".oct"
              hidden
              onChange={handleFileChange}
            />
            <Box>
              <Button
                variant="contained"
                color="secondary"
                startIcon={<Upload />}
                onClick={() => fileInputRef.current?.click()}
                disabled={importing}
              >
                {importing
                  ? t("settings.dataImport.importing")
                  : t("settings.dataImport.button")}
              </Button>
            </Box>
          </Stack>
        </CardContent>
      </Card>
    </>
  );
}
