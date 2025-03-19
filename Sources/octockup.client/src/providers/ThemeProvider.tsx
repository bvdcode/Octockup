import { useState, ReactNode } from "react";
import { darkTheme } from "../themes/darkTheme";
import { ToastContainer } from "react-toastify";
import { lightTheme } from "../themes/lightTheme";
import { ThemeContext } from "../contexts/ThemeContext";
import { CssBaseline, ThemeProvider as MuiThemeProvider } from "@mui/material";

export const ThemeProvider = ({ children }: { children: ReactNode }) => {
  const [isDarkMode, setIsDarkMode] = useState(() => {
    const saved = localStorage.getItem("theme");
    return saved
      ? saved === "dark"
      : window.matchMedia("(prefers-color-scheme: dark)").matches;
  });

  const theme = isDarkMode ? darkTheme : lightTheme;
  const toggleTheme = () => {
    setIsDarkMode((prev) => {
      const newMode = !prev;
      localStorage.setItem("theme", newMode ? "dark" : "light");
      return newMode;
    });
  };

  return (
    <ThemeContext.Provider value={{ isDarkMode, toggleTheme, theme }}>
      <CssBaseline />
      <ToastContainer theme={isDarkMode ? "dark" : "light"} />
      <MuiThemeProvider theme={theme}>{children}</MuiThemeProvider>
    </ThemeContext.Provider>
  );
};
