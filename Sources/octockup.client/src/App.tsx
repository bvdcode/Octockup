import { User } from "./api/types";
import RoutesComponent from "./Routes";
import { useSystemTheme } from "./theme";
import AuthProvider from "react-auth-kit";
import { useEffect, useState } from "react";
import { refresh } from "./auth/AuthKitMethods";
import { ToastContainer } from "react-toastify";
import CssBaseline from "@mui/material/CssBaseline";
import createStore from "react-auth-kit/createStore";
import { ThemeProvider } from "@mui/material/styles";

function App() {
  const systemTheme = useSystemTheme();
  const [theme, setTheme] = useState(systemTheme);
  const store = createStore<User>({
    authName: "_octockup_auth",
    authType: "localstorage",
    refresh: refresh,
  });

  useEffect(() => {
    setTheme(systemTheme);
  }, [systemTheme]);

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
