import styles from "./HomePage.module.css";
import {
  BackupInfo,
  CreateJob,
  Dashboard,
  NavBar,
  Profile,
} from "../components";
import { Routes, Route, Navigate, useLocation } from "react-router-dom";
import { Add, Dashboard as DashIcon, Person } from "@mui/icons-material";

const HomePage: React.FC = () => {
  const location = useLocation();

  const navButtons = [
    {
      path: "/dashboard",
      icon: <DashIcon sx={{ fontSize: 45 }} />,
    },
    {
      path: "/create",
      icon: <Add sx={{ fontSize: 45 }} />,
    },
    {
      path: "/profile",
      icon: <Person sx={{ fontSize: 45 }} />,
    },
  ];

  return (
    <div className={styles.homePageContainer}>
      <Routes key={location.pathname}>
        <Route path="/profile" element={<Profile />} />
        <Route path="/create" element={<CreateJob />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/backups/:id" element={<BackupInfo />} />
        <Route path="/" element={<Navigate to="/dashboard" />} />
      </Routes>
      <NavBar buttons={navButtons} />
    </div>
  );
};

export default HomePage;
