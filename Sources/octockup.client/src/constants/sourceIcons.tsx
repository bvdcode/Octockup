import React from "react";
import { Avatar } from "@mui/material";
import { Folder, HelpOutline } from "@mui/icons-material";

// Mapping of backend source IDs to icons. Extend manually as needed.
const ICONS: Record<string, React.ReactNode> = {
  "Octockup.Server.Modules.FileSystemBackupSource": <Folder />,
  "Octockup.Server.Modules.S3BackupStorage": <Avatar src="/s3.svg" sx={{ p: 1 }} />,
};

export function getSourceIcon(id: string): React.ReactNode {
  return ICONS[id] ?? <HelpOutline />;
}

export function listKnownSourceIds(): string[] {
  return Object.keys(ICONS);
}
