import styles from "./HomePage.module.css";
import { Routes, Route } from "react-router-dom";
import { Dashboard, NavBar, Profile } from "../components";

const HomePage: React.FC = () => {
  const navButtons = [{ path: "/dashboard", icon: <button></button> }];

  return (
    <div className={styles.container}>
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
