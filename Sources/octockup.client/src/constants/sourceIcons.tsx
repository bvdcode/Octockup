import React from "react";
import { Folder, HelpOutline } from "@mui/icons-material";

// Mapping of backend source IDs to icons. Extend manually as needed.
const ICONS: Record<string, React.ReactNode> = {
  "Octockup.Server.BackupSources.FileSystemBackupSource": <Folder />,
};

export function getSourceIcon(id: string): React.ReactNode {
  return ICONS[id] ?? <HelpOutline />;
}

export function listKnownSourceIds(): string[] {
  return Object.keys(ICONS);
}
