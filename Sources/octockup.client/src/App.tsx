import { User } from "./api/types";
import RoutesComponent from "./Routes";
import { useSystemTheme } from "./theme";
import AuthProvider from "react-auth-kit";
import { refresh } from "./auth/AuthKitMethods";
import { ToastContainer } from "react-toastify";
import CssBaseline from "@mui/material/CssBaseline";
import createStore from "react-auth-kit/createStore";
import { useEffect, useMemo, useState } from "react";
import { ThemeProvider, createTheme } from "@mui/material/styles";

function App() {
  const systemTheme = useSystemTheme();
  const [theme, setTheme] = useState(createTheme(systemTheme));
  const store = createStore<User>({
    authName: "_octockup_auth",
    authType: "localstorage",
    refresh: refresh,
  });

  const memoizedTheme = useMemo(() => createTheme(systemTheme), [systemTheme]);

  useEffect(() => {
    setTheme(memoizedTheme);
  }, [memoizedTheme]);

  return (
    <ThemeProvider theme={theme}>
      <AuthProvider store={store}>
        <CssBaseline />
        <ToastContainer />
        <RoutesComponent />
      </AuthProvider>
    </ThemeProvider>
  );
}

export default App;
