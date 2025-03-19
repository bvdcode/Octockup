import RoutesComponent from "./Routes";
import AuthProvider from "./auth/AuthProvider";
import { refreshAccessToken } from "./api/api";
import { ThemeProvider } from "./providers/ThemeProvider";

function App() {
  async function refresh(refreshToken: string) {
    const tokens = await refreshAccessToken(refreshToken);
    return {
      newUserState: tokens.user,
      newAccessToken: tokens.accessToken,
      newRefreshToken: tokens.refreshToken,
    };
  }

  return (
    <ThemeProvider>
      <AuthProvider
        refresh={refresh}
        storageKey="_octockup_auth"
        refreshIntervalSeconds={"auto"}
      >
        <RoutesComponent />
      </AuthProvider>
    </ThemeProvider>
  );
}

export default App;
