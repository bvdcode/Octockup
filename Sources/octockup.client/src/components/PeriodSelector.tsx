import React from "react";
import { useTranslation } from "react-i18next";
import { Box, InputLabel, MenuItem, Select } from "@mui/material";

interface PeriodSelectorProps {
  onSelectPeriod: (period: number) => void;
}

const PeriodSelector: React.FC<PeriodSelectorProps> = () => {
  const { t } = useTranslation();

  return (
    <Box>
      <InputLabel id="period-label">{t("createBackup.period")}</InputLabel>
      <Select labelId="period-label" value={undefined} fullWidth>
        <MenuItem value={1}>{t("createBackup.daily")}</MenuItem>
        <MenuItem value={7}>{t("createBackup.weekly")}</MenuItem>
        <MenuItem value={30}>{t("createBackup.monthly")}</MenuItem>
        <MenuItem value={365}>{t("createBackup.yearly")}</MenuItem>
        <MenuItem value={0}>{t("createBackup.once")}</MenuItem>
        <MenuItem value={-1}>{t("createBackup.custom")}</MenuItem>
      </Select>
    </Box>
  );
};

export default PeriodSelector;
