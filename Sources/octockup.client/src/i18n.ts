import i18n from "i18next";
import en from "./locales/en.json";
import ru from "./locales/ru.json";
import { initReactI18next } from "react-i18next";
import { LOCAL_STORAGE_LANGUAGE_KEY } from "./config";

const savedLanguage = localStorage.getItem(LOCAL_STORAGE_LANGUAGE_KEY) || "en";

i18n.use(initReactI18next).init({
  resources: {
    en: {
      translation: en,
    },
    ru: {
      translation: ru,
    },
  },
  lng: savedLanguage,
  fallbackLng: "en",
  interpolation: {
    escapeValue: false,
  },
});

export default i18n;
