import { AddCircleOutline, Search } from "@mui/icons-material";
import {
  Box,
  Button,
  IconButton,
  InputAdornment,
  MenuItem,
  TextField,
  Tooltip,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { BackupSortOption } from "../../types/backupList";

interface StorageOption {
  id: string;
  tag: string;
}

interface BackupListToolbarProps {
  search: string;
  selectedStorageId: string | null;
  sort: BackupSortOption;
  storages: StorageOption[];
  onCreate: () => void;
  onSearchChange: (value: string) => void;
  onSortChange: (value: BackupSortOption) => void;
  onStorageChange: (value: string | null) => void;
}

export function BackupListToolbar({
  search,
  selectedStorageId,
  sort,
  storages,
  onCreate,
  onSearchChange,
  onSortChange,
  onStorageChange,
}: BackupListToolbarProps) {
  const { t } = useTranslation();

  return (
    <Box
      display="grid"
      gap={1}
      sx={(theme) => ({
        backgroundColor: "background.default",
        gridTemplateColumns: {
          xs: "minmax(0, 1fr) minmax(0, 1fr) auto",
          sm: "minmax(160px, 1fr) 150px 190px auto",
        },
        position: "sticky",
        top: theme.spacing(-2),
        zIndex: theme.zIndex.appBar - 1,
        py: 1,
      })}
    >
      <TextField
        size="small"
        value={search}
        placeholder={t("backups.searchPlaceholder")}
        aria-label={t("backups.searchPlaceholder")}
        onChange={(event) => onSearchChange(event.target.value)}
        sx={{
          gridColumn: { xs: "1 / 3", sm: "auto" },
          gridRow: { xs: 1, sm: "auto" },
          "& input::placeholder": {
            color: "text.secondary",
            opacity: 1,
          },
        }}
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <Search fontSize="small" />
              </InputAdornment>
            ),
          },
        }}
      />
      <TextField
        select
        size="small"
        label={t("backups.storage")}
        value={selectedStorageId ?? "all"}
        onChange={(event) =>
          onStorageChange(
            event.target.value === "all" ? null : event.target.value,
          )
        }
        sx={{
          gridColumn: { xs: "1 / 2", sm: "auto" },
          gridRow: { xs: 2, sm: "auto" },
        }}
      >
        <MenuItem value="all">{t("backups.allStorages")}</MenuItem>
        {storages.map((storage) => (
          <MenuItem key={storage.id} value={storage.id}>
            {storage.tag}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        select
        size="small"
        label={t("backups.sort.label")}
        value={sort}
        onChange={(event) =>
          onSortChange(event.target.value as BackupSortOption)
        }
        sx={{
          gridColumn: { xs: "2 / -1", sm: "auto" },
          gridRow: { xs: 2, sm: "auto" },
        }}
      >
        {Object.values(BackupSortOption).map((option) => (
          <MenuItem key={option} value={option}>
            {t(`backups.sort.${option}`)}
          </MenuItem>
        ))}
      </TextField>
      <Button
        variant="contained"
        startIcon={<AddCircleOutline />}
        onClick={onCreate}
        sx={{
          gridColumn: { xs: "1 / -1", sm: "auto" },
          display: { xs: "none", sm: "inline-flex" },
          whiteSpace: "nowrap",
        }}
      >
        {t("backups.newBackup")}
      </Button>
      <Tooltip title={t("backups.newBackup")}>
        <IconButton
          color="primary"
          aria-label={t("backups.newBackup")}
          onClick={onCreate}
          sx={{
            display: { xs: "inline-flex", sm: "none" },
            gridColumn: 3,
            gridRow: 1,
          }}
        >
          <AddCircleOutline />
        </IconButton>
      </Tooltip>
    </Box>
  );
}
