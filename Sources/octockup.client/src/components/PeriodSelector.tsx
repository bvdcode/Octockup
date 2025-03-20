import React from "react";
import { useTranslation } from "react-i18next";
import { FormControl, InputLabel, MenuItem, Select } from "@mui/material";

interface PeriodSelectorProps {
  onSelectPeriod: (period: number) => void;
}

const PeriodSelector: React.FC<PeriodSelectorProps> = ({ onSelectPeriod }) => {
  const { t } = useTranslation();

  return (
    <FormControl fullWidth>
      <InputLabel id="period-label">{t("createBackup.period")}</InputLabel>
      <Select
        labelId="period-label"
        sx={{
          marginTop: 1,
        }}
        value={undefined}
        onChange={(event) => {
          if (!event.target.value) {
            return;
          }
          const selectedPeriod = Number(event.target.value);
          if (selectedPeriod) {
            onSelectPeriod(selectedPeriod);
          }
        }}
      >
        <MenuItem value={1}>{t("createBackup.daily")}</MenuItem>
        <MenuItem value={7}>{t("createBackup.weekly")}</MenuItem>
        <MenuItem value={30}>{t("createBackup.monthly")}</MenuItem>
        <MenuItem value={365}>{t("createBackup.yearly")}</MenuItem>
        <MenuItem value={0}>{t("createBackup.once")}</MenuItem>
        <MenuItem value={-1}>{t("createBackup.custom")}</MenuItem>
      </Select>
    </FormControl>
  );
};

export default PeriodSelector;
