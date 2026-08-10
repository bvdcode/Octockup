import { Box, MenuItem, Paper, TextField } from "@mui/material";
import { getSourceIcon } from "../../constants/sourceIcons";
import type { Module } from "../../types/api";

interface BackupModuleSelectorProps {
  label: string;
  modules: Module[];
  value: string;
  onChange: (value: string) => void;
}

export function BackupModuleSelector({
  label,
  modules,
  value,
  onChange,
}: BackupModuleSelectorProps) {
  const selectedModule = modules.find((module) => module.id === value);
  return (
    <Paper
      variant="outlined"
      sx={{
        p: 3,
        flex: "1 1 auto",
        textAlign: "center",
        minWidth: 280,
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-between",
        alignItems: "center",
      }}
    >
      <Box
        sx={{
          fontSize: 96,
          lineHeight: 1,
          mb: 2,
          minHeight: 96,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        {getSourceIcon(selectedModule?.backupModuleId ?? "")}
      </Box>
      <TextField
        select
        label={label}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        fullWidth
        sx={{ maxWidth: 400 }}
      >
        {modules.map((module) => (
          <MenuItem key={module.id} value={module.id}>
            {module.tag}
          </MenuItem>
        ))}
      </TextField>
    </Paper>
  );
}
