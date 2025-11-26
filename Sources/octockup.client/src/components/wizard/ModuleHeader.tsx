import { Box, Card, CardContent, Stack, Typography } from "@mui/material";
import { getSourceIcon } from "../../constants/sourceIcons";
import type { BackupSource, BackupStorage } from "../../types/api";

interface ModuleHeaderProps {
  moduleMeta: BackupSource | BackupStorage;
}

export function ModuleHeader({ moduleMeta }: ModuleHeaderProps) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack direction="row" spacing={2} alignItems="center">
          <Box sx={{ fontSize: 42 }}>{getSourceIcon(moduleMeta.id)}</Box>
          <Box>
            <Typography variant="h6">{moduleMeta.name}</Typography>
            <Typography variant="caption" color="text.secondary">
              {moduleMeta.id}
            </Typography>
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
