import { AddCircleOutline, Search } from "@mui/icons-material";
import {
  Box,
  Button,
  IconButton,
  InputAdornment,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { BackupSortOption } from "../../types/backupList";
import { formatSize } from "../../utils/formatUtils";
import {
  getInfoTextColor,
  getWarningTextColor,
} from "../../utils/themeColors";

interface StorageOption {
  id: string;
  tag: string;
}

interface BackupListToolbarProps {
  backupCount: number;
  issueCount: number;
  logicalSize: number;
  runningCount: number;
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
  backupCount,
  issueCount,
  logicalSize,
  runningCount,
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
    <Paper
      variant="outlined"
      sx={(theme) => ({
        backgroundColor: "background.paper",
        position: "sticky",
        top: 0,
        zIndex: theme.zIndex.appBar - 1,
        p: 1.5,
      })}
    >
      <Stack spacing={1}>
        <Box
          display="grid"
          gap={1}
          sx={{
            gridTemplateColumns: {
              xs: "minmax(0, 1fr) minmax(0, 1fr) auto",
              sm: "minmax(160px, 1fr) 150px 190px auto",
            },
          }}
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
              onStorageChange(event.target.value === "all" ? null : event.target.value)
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
        <Stack
          direction="row"
          spacing={{ xs: 1.25, sm: 2 }}
          flexWrap={{ xs: "nowrap", sm: "wrap" }}
          overflow="auto"
          useFlexGap
        >
          <Typography
            variant="caption"
            color="text.secondary"
            display={{ xs: "none", sm: "block" }}
          >
            {t("backups.summary.backups", { count: backupCount })}
          </Typography>
          <Typography
            variant="caption"
            color="text.secondary"
            whiteSpace="nowrap"
          >
            {t("backups.summary.logicalSize", {
              size: formatSize(logicalSize),
            })}
          </Typography>
          {runningCount > 0 && (
            <Typography
              variant="caption"
              sx={{
                color: getInfoTextColor,
                whiteSpace: "nowrap",
              }}
            >
              {t("backups.summary.running", { count: runningCount })}
            </Typography>
          )}
          {issueCount > 0 && (
            <Typography
              variant="caption"
              sx={{
                color: getWarningTextColor,
                whiteSpace: "nowrap",
              }}
            >
              {t("backups.summary.issues", { count: issueCount })}
            </Typography>
          )}
        </Stack>
      </Stack>
    </Paper>
  );
}
