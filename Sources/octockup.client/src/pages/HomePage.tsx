import styles from "./HomePage.module.css";
import { Routes, Route } from "react-router-dom";
import { Dashboard, NavBar, Profile } from "../components";
import { Dashboard as DashIcon, Person } from "@mui/icons-material";

const HomePage: React.FC = () => {
  const navButtons = [
    {
      path: "/dashboard",
      icon: <DashIcon />,
    },
    {
      path: "/profile",
      icon: <Person />,
    },
  ];

  return (
    <div className={styles.homePageContainer}>
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/profile" element={<Profile />} />
        <Route path="/dashboard" element={<Dashboard />} />
      </Routes>
      <NavBar buttons={navButtons} />
    </div>
  );
};

export default HomePage;
