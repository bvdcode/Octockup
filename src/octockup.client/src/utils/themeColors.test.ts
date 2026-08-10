import { createTheme, getContrastRatio } from "@mui/material/styles";
import { describe, expect, it } from "vitest";
import type { BackupOverallStatus } from "./backupUtils";
import {
  getInfoTextColor,
  getStatusChipColors,
  getWarningTextColor,
} from "./themeColors";

const MINIMUM_TEXT_CONTRAST = 4.5;
const customizedStatuses: BackupOverallStatus[] = [
  "running",
  "failed",
  "warning",
  "scheduled",
];

describe("themeColors", () => {
  it.each(["light", "dark"] as const)(
    "keeps semantic summary text readable in %s mode",
    (mode) => {
      const theme = createTheme({ palette: { mode } });
      const paper = theme.palette.background.paper;

      expect(getContrastRatio(getInfoTextColor(theme), paper)).toBeGreaterThanOrEqual(
        MINIMUM_TEXT_CONTRAST,
      );
      expect(
        getContrastRatio(getWarningTextColor(theme), paper),
      ).toBeGreaterThanOrEqual(MINIMUM_TEXT_CONTRAST);
    },
  );

  it.each(["light", "dark"] as const)(
    "keeps customized status chips readable in %s mode",
    (mode) => {
      const theme = createTheme({ palette: { mode } });

      customizedStatuses.forEach((status) => {
        const colors = getStatusChipColors(status, theme);
        if (colors === null) {
          return;
        }
        expect(
          getContrastRatio(colors.color, colors.backgroundColor),
        ).toBeGreaterThanOrEqual(MINIMUM_TEXT_CONTRAST);
      });
    },
  );
});
