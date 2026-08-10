import type { Theme } from "@mui/material";
import { darken } from "@mui/material/styles";
import type { BackupOverallStatus } from "./backupUtils";

export interface SemanticColors {
  backgroundColor: string;
  color: string;
}

export function getInfoTextColor(theme: Theme): string {
  switch (theme.palette.mode) {
    case "light":
      return theme.palette.info.dark;
    case "dark":
      return theme.palette.info.main;
  }
}

export function getWarningTextColor(theme: Theme): string {
  switch (theme.palette.mode) {
    case "light":
      return darken(theme.palette.warning.dark, 0.2);
    case "dark":
      return theme.palette.warning.main;
  }
}

export function getStatusChipColors(
  status: BackupOverallStatus,
  theme: Theme,
): SemanticColors | null {
  switch (status) {
    case "running":
      return getRunningChipColors(theme);
    case "failed":
      return {
        backgroundColor: theme.palette.error.dark,
        color: theme.palette.common.white,
      };
    case "warning":
    case "scheduled":
      return {
        backgroundColor: theme.palette.warning.main,
        color: theme.palette.common.black,
      };
    case "success":
    case "created":
    case "idle":
      return null;
  }
}

function getRunningChipColors(theme: Theme): SemanticColors {
  switch (theme.palette.mode) {
    case "light":
      return {
        backgroundColor: theme.palette.info.dark,
        color: theme.palette.common.white,
      };
    case "dark":
      return {
        backgroundColor: theme.palette.info.main,
        color: theme.palette.common.black,
      };
  }
}
