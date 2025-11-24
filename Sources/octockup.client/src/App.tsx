import {
  Home,
  Backup,
  Schedule,
  CloudDone,
  CloudDownload,
} from "@mui/icons-material";
import * as locales from "./locales";
import HomePage from "./pages/Home";
import SourcesPage from "./pages/Sources";
import StoragesPage from "./pages/Storages";
import SourceWizard from "./pages/SourceWizard";
import StorageWizard from "./pages/StorageWizard";
import { AppShell, type TokenPair, type UserInfo } from "@bvdcode/react-kit";

function App() {
  return (
    <AppShell
      appName="Octockup"
      logoUrl="/octockup.png"
      contentMaxWidth={1200}
      translations={{
        en: { translation: locales.en },
        ru: { translation: locales.ru },
      }}
      authConfig={{
        usernamePattern: /^[a-zA-Z0-9._-]+$/,
        login: async (credentials, axiosInstance) => {
          const response = await axiosInstance.post<TokenPair>(
            "/api/v1/auth/login",
            credentials,
          );
          return response.data;
        },
        getUserInfo: async (axiosInstance) => {
          const response = await axiosInstance.get<UserInfo>("/api/v1/auth/me");
          response.data.avatarUrl = "/octockup.png";
          return response.data;
        },
        refreshToken: async (refreshToken, axiosInstance) => {
          const response = await axiosInstance.post<TokenPair>(
            "/api/v1/auth/refresh",
            { refreshToken },
          );
          return response.data;
        },
      }}
      pages={[
        {
          icon: <Home />,
          name: "Home",
          route: "/",
          component: <HomePage />,
        },
        {
          icon: <Backup />,
          name: "Sources",
          route: "/sources",
          component: <SourcesPage />,
        },
        {
          route: "/sources/new",
          component: <SourceWizard />,
        },
        {
          icon: <CloudDownload />,
          name: "Storages",
          route: "/storages",
          component: <StoragesPage />,
        },
        {
          route: "/storages/new",
          component: <StorageWizard />,
        },
        {
          icon: <CloudDone />,
          name: "Tasks",
          route: "/tasks",
          component: <div>Tasks Page</div>,
        },
        {
          icon: <Schedule />,
          name: "Schedules",
          route: "/schedules",
          component: <div>Schedules Page</div>,
        },
      ]}
    />
  );
}

export default App;
