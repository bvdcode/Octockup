import styles from "./HomePage.module.css";
import { Routes, Route, Navigate } from "react-router-dom";
import { CreateJob, Dashboard, NavBar, Profile } from "../components";
import { Add, Dashboard as DashIcon, Person } from "@mui/icons-material";

const HomePage: React.FC = () => {
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
      <Routes>
        <Route path="/profile" element={<Profile />} />
        <Route path="/create" element={<CreateJob />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/" element={<Navigate to="/dashboard" />} />
      </Routes>
      <NavBar buttons={navButtons} />
    </div>
  );
};

export default HomePage;
