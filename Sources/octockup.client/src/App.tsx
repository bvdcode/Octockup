import { AppShell } from "@bvdcode/react-kit";
import {
  Backup,
  CloudDone,
  CloudDownload,
  Home,
  Schedule,
} from "@mui/icons-material";

function App() {
  return (
    <AppShell
      appName="Octockup"
      logoUrl="/octockup.png"
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
