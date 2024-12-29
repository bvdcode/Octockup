import RoutesComponent from "./Routes";
import { useSystemTheme } from "./theme";
import AuthProvider from "./auth/AuthProvider";
import { refreshAccessToken } from "./api/api";
import { ToastContainer } from "react-toastify";
import CssBaseline from "@mui/material/CssBaseline";
import { useEffect, useMemo, useState } from "react";
import { ThemeProvider, createTheme } from "@mui/material/styles";

function App() {
  const systemTheme = useSystemTheme();
  const [theme, setTheme] = useState(createTheme(systemTheme));
  const memoizedTheme = useMemo(() => createTheme(systemTheme), [systemTheme]);

  useEffect(() => {
    setTheme(memoizedTheme);
  }, [memoizedTheme]);

  async function refresh(refreshToken: string) {
    const tokens = await refreshAccessToken(refreshToken);
    return {
      newUserState: tokens.user,
      newAccessToken: tokens.accessToken,
      newRefreshToken: tokens.refreshToken,
    };
  }

  return (
    <ThemeProvider theme={theme}>
      <AuthProvider
        storageKey="_octockup_auth"
        refresh={refresh}
        refreshIntervalSeconds={60}
      >
        <CssBaseline />
        <ToastContainer />
        <RoutesComponent />
      </AuthProvider>
    </ThemeProvider>
  );
}

export default App;
