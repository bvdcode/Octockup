import React from "react";
import { useTranslation } from "react-i18next";
import { LOCAL_STORAGE_LANGUAGE_KEY } from "../../config";
import { Select, MenuItem, SelectChangeEvent } from "@mui/material";

const LanguageSwitcher: React.FC = () => {
  const { i18n } = useTranslation();

  const changeLanguage = (event: SelectChangeEvent<string>) => {
    i18n.changeLanguage(event.target.value as string);
    localStorage.setItem(LOCAL_STORAGE_LANGUAGE_KEY, event.target.value);
  };

  const supportedLanguages = [
    { code: "en", name: "English" },
    { code: "ru", name: "Русский" },
  ];

  return (
    <Select value={i18n.language} onChange={changeLanguage}>
      {supportedLanguages.map((lang) => (
        <MenuItem key={lang.code} value={lang.code}>
          {lang.name}
        </MenuItem>
      ))}
    </Select>
  );
};

export default LanguageSwitcher;
