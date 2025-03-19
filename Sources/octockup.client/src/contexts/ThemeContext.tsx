import { createContext } from "react";
import { Theme } from "@mui/material/styles";

interface ThemeContextType {
  isDarkMode: boolean;
  toggleTheme: () => void;
  theme: Theme;
}

export const ThemeContext = createContext<ThemeContextType | null>(null);
