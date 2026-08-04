import {
  Home,
  Backup,
  Schedule,
  CloudDone,
  CloudDownload,
  GitHub,
  Settings,
} from "@mui/icons-material";
import * as locales from "./locales";
import HomePage from "./pages/Home";
import SourcesPage from "./pages/Sources";
import StoragesPage from "./pages/Storages";
import BackupsPage from "./pages/Backups";
import BackupWizard from "./pages/BackupWizard";
import SourceWizard from "./pages/SourceWizard";
import StorageWizard from "./pages/StorageWizard";
import { AppShell, type TokenPair } from "@bvdcode/react-kit";
import SchedulesPage from "./pages/Schedules";
import ScheduleWizard from "./pages/ScheduleWizard";
import SnapshotsPage from "./pages/Snapshots";
import SnapshotFilesPage from "./pages/SnapshotFiles";
import SettingsPage from "./pages/Settings";
import { Fab } from "@mui/material";
import LoginPage from "./pages/Login";
import type { CurrentUser } from "./types/auth";
import { toCookieSession } from "./utils/authSession";

function App() {
  return (
    <>
      <AppShell
        appName="Octockup"
        logoUrl="/octockup.png"
        contentMaxWidth={800}
        translations={{
          en: { translation: locales.en },
          ru: { translation: locales.ru },
        }}
        renderLoginPage={() => <LoginPage />}
        authConfig={{
          usernamePattern: /^[a-zA-Z0-9._-]+$/,
          login: async (credentials, axiosInstance) => {
            const response = await axiosInstance.post<TokenPair>(
              "/api/v1/auth/login",
              credentials,
            );
            return toCookieSession(response.data);
          },
          getUserInfo: async (axiosInstance) => {
            const response = await axiosInstance.get<CurrentUser>(
              "/api/v1/auth/me",
            );
            response.data.avatarUrl = "/octockup.png";
            return response.data;
          },
          refreshToken: async (_refreshToken, axiosInstance) => {
            const response = await axiosInstance.post<TokenPair>(
              "/api/v1/auth/refresh",
              { refreshToken: "" },
            );
            return toCookieSession(response.data);
          },
          logout: async (_refreshToken, axiosInstance) => {
            await axiosInstance.post("/api/v1/auth/logout");
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
            route: "/storages/new",
            component: <StorageWizard />,
          },
          {
            icon: <CloudDone />,
            name: "Backups",
            route: "/backups",
            component: <BackupsPage />,
          },
          {
            icon: <CloudDownload />,
            name: "Storages",
            route: "/storages",
            component: <StoragesPage />,
          },
          {
            route: "/backups/new",
            component: <BackupWizard />,
          },
          {
            route: "/backups/:backupId/snapshots",
            component: <SnapshotsPage />,
          },
          {
            route: "/backups/:backupId/snapshots/:snapshotId/files",
            component: <SnapshotFilesPage />,
          },
          {
            icon: <Schedule />,
            name: "Schedules",
            route: "/schedules",
            component: <SchedulesPage />,
          },
          {
            route: "/schedules/new",
            component: <ScheduleWizard />,
          },
          {
            icon: <Settings />,
            name: "Settings",
            route: "/settings",
            component: <SettingsPage />,
          },
        ]}
      />
      <Fab
        color="primary"
        aria-label="add"
        sx={{ position: "fixed", bottom: 16, right: 16 }}
        href="https://github.com/bvdcode/octockup"
        target="_blank"
        rel="noopener noreferrer"
      >
        <GitHub />
      </Fab>
    </>
  );
}

export default App;
