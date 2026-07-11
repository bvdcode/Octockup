import {
  Box,
  Button,
  Divider,
  FormControl,
  MenuItem,
  Select,
  Typography,
} from "@mui/material";
import { AddCircleOutline } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import { formatSize } from "../../utils/formatUtils";

interface StorageOption {
  id: string;
  tag: string;
}

interface BackupsHeaderProps {
  totalFiles: number;
  totalSize: number;
  storages: StorageOption[];
  selectedStorageId: string | null;
  onStorageChange: (storageId: string | null) => void;
  onNewBackup: () => void;
}

export function BackupsHeader({
  totalFiles,
  totalSize,
  storages,
  selectedStorageId,
  onStorageChange,
  onNewBackup,
}: BackupsHeaderProps) {
  const { t } = useTranslation();

  return (
    <Box
      display="flex"
      flexDirection={{ xs: "column", md: "row" }}
      alignItems={{ xs: "stretch", md: "center" }}
      justifyContent="space-between"
      gap={2}
    >
      <Box display="flex" alignItems="center" gap={2} flexWrap="wrap">
        <Typography variant="h5">{t("backups.title")}</Typography>
        <Divider orientation="vertical" flexItem />
        <Typography variant="body2" color="text.secondary">
          {t("backups.totalFiles", { count: totalFiles })}
        </Typography>
        <Divider orientation="vertical" flexItem />
        <Typography variant="body2" color="text.secondary">
          {t("backups.totalSize", { size: formatSize(totalSize) })}
        </Typography>
      </Box>
      <Box
        display="flex"
        flexDirection={{ xs: "column", sm: "row" }}
        alignItems="stretch"
        gap={2}
      >
        <FormControl
          size="small"
          sx={{ minWidth: 150, width: { xs: "100%", sm: "auto" } }}
        >
          <Select
            value={selectedStorageId ?? "all"}
            onChange={(event) =>
              onStorageChange(
                event.target.value === "all" ? null : event.target.value,
              )
            }
            displayEmpty
          >
            <MenuItem value="all">{t("backups.allStorages")}</MenuItem>
            {storages.map((storage) => (
              <MenuItem key={storage.id} value={storage.id}>
                {storage.tag}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <Button
          variant="contained"
          startIcon={<AddCircleOutline />}
          onClick={onNewBackup}
        >
          {t("backups.newBackup")}
        </Button>
      </Box>
    </Box>
  );
}
