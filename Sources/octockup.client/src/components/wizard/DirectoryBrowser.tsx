import {
  Box,
  Button,
  CircularProgress,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import { ArrowRight, ArrowUpward, Folder, Home } from "@mui/icons-material";
import { useTranslation } from "react-i18next";

interface DirectoryBrowserProps {
  browserPath: string;
  browserDirs: string[];
  browserLoading: boolean;
  onNavigateToRoot: () => void;
  onNavigateUp: () => void;
  onNavigateToDir: (dir: string) => void;
  disabled?: boolean;
}

export function DirectoryBrowser({
  browserPath,
  browserDirs,
  browserLoading,
  onNavigateToRoot,
  onNavigateUp,
  onNavigateToDir,
  disabled,
}: DirectoryBrowserProps) {
  const { t } = useTranslation();

  return (
    <Paper variant="outlined" sx={{ p: 2, maxHeight: 300, overflow: "auto" }}>
      <Stack spacing={1}>
        <Stack direction="row" alignItems="center" justifyContent="space-between">
          <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
            <IconButton size="small" onClick={onNavigateToRoot} disabled={disabled}>
              <Home fontSize="small" />
            </IconButton>
            <ArrowRight fontSize="small" />
            <Typography variant="caption" color="text.secondary">
              {browserPath || "/"}
            </Typography>
          </Box>
          {browserPath && (
            <Button
              size="small"
              startIcon={<ArrowUpward />}
              onClick={onNavigateUp}
              disabled={disabled}
            >
              {t("wizard.up")}
            </Button>
          )}
        </Stack>
        {browserLoading ? (
          <Box display="flex" justifyContent="center" p={2}>
            <CircularProgress size={24} />
          </Box>
        ) : browserDirs.length === 0 ? (
          <Typography
            variant="body2"
            color="text.secondary"
            textAlign="center"
            py={2}
          >
            {browserPath ? t("wizard.noSubdirectories") : t("wizard.clickToLoad")}
          </Typography>
        ) : (
          <List dense>
            {browserDirs.map((dir) => (
              <ListItem key={dir} disablePadding>
                <ListItemButton onClick={() => onNavigateToDir(dir)} disabled={disabled}>
                  <ListItemIcon sx={{ minWidth: 36 }}>
                    <Folder />
                  </ListItemIcon>
                  <ListItemText primary={dir} />
                </ListItemButton>
              </ListItem>
            ))}
          </List>
        )}
      </Stack>
    </Paper>
  );
}
