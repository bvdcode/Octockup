import {
  Home,
  Backup,
  Schedule,
  CloudDone,
  CloudDownload,
  CleaningServices,
  GitHub,
  Settings,
} from "@mui/icons-material";
import { Box, CircularProgress, Fab } from "@mui/material";
import { lazy, Suspense, type ReactNode } from "react";
import * as locales from "./locales";
import { AppShell, type TokenPair, type UserInfo } from "@bvdcode/react-kit";

const HomePage = lazy(() => import("./pages/Home"));
const SourcesPage = lazy(() => import("./pages/Sources"));
const StoragesPage = lazy(() => import("./pages/Storages"));
const BackupsPage = lazy(() => import("./pages/Backups"));
const BackupWizard = lazy(() => import("./pages/BackupWizard"));
const SourceWizard = lazy(() => import("./pages/SourceWizard"));
const StorageWizard = lazy(() => import("./pages/StorageWizard"));
const SchedulesPage = lazy(() => import("./pages/Schedules"));
const ScheduleWizard = lazy(() => import("./pages/ScheduleWizard"));
const SnapshotsPage = lazy(() => import("./pages/Snapshots"));
const SnapshotFilesPage = lazy(() => import("./pages/SnapshotFiles"));
const SettingsPage = lazy(() => import("./pages/Settings"));
const StorageMaintenancePage = lazy(
  () => import("./pages/StorageMaintenance"),
);

function renderPage(page: ReactNode) {
  return (
    <Suspense
      fallback={
        <Box display="flex" justifyContent="center" padding={4}>
          <CircularProgress />
        </Box>
      }
    >
      {page}
    </Suspense>
  );
}

function App() {
  return (
    <>
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
            const response = await axiosInstance.get<UserInfo>(
              "/api/v1/auth/me",
            );
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
            component: renderPage(<HomePage />),
          },
          {
            icon: <Backup />,
            name: "Sources",
            route: "/sources",
            component: renderPage(<SourcesPage />),
          },
          {
            route: "/sources/new",
            component: renderPage(<SourceWizard />),
          },
          {
            route: "/storages/new",
            component: renderPage(<StorageWizard />),
          },
          {
            icon: <CloudDone />,
            name: "Backups",
            route: "/backups",
            component: renderPage(<BackupsPage />),
          },
          {
            icon: <CloudDownload />,
            name: "Storages",
            route: "/storages",
            component: renderPage(<StoragesPage />),
          },
          {
            icon: <CleaningServices />,
            name: "Maintenance",
            route: "/storage-maintenance",
            component: renderPage(<StorageMaintenancePage />),
          },
          {
            route: "/backups/new",
            component: renderPage(<BackupWizard />),
          },
          {
            route: "/backups/:backupId/snapshots",
            component: renderPage(<SnapshotsPage />),
          },
          {
            route: "/backups/:backupId/snapshots/:snapshotId/files",
            component: renderPage(<SnapshotFilesPage />),
          },
          {
            icon: <Schedule />,
            name: "Schedules",
            route: "/schedules",
            component: renderPage(<SchedulesPage />),
          },
          {
            route: "/schedules/new",
            component: renderPage(<ScheduleWizard />),
          },
          {
            icon: <Settings />,
            name: "Settings",
            route: "/settings",
            component: renderPage(<SettingsPage />),
          },
        ]}
      />
      <Fab
        color="primary"
        aria-label="add"
        sx={{
          position: "fixed",
          bottom: 16,
          right: 16,
          display: { xs: "none", sm: "inline-flex" },
        }}
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
