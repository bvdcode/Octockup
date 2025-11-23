import {
  Home,
  Backup,
  Schedule,
  CloudDone,
  CloudDownload,
} from "@mui/icons-material";
import { AppShell, type TokenPair, type UserInfo } from "@bvdcode/react-kit";

function App() {
  return (
    <AppShell
      appName="Octockup"
      logoUrl="/octockup.png"
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
          component: <div>Home Page</div>,
        },
        {
          icon: <Backup />,
          name: "Sources",
          route: "/sources",
          component: <div>Sources Page</div>,
        },
        {
          icon: <CloudDownload />,
          name: "Storages",
          route: "/storages",
          component: <div>Storages Page</div>,
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
