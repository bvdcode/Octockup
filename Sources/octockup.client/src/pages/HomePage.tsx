import { Routes, Route } from "react-router-dom";
import { Dashboard, Profile } from "../components";

const HomePage: React.FC = () => {
  return (
    <Routes>
      <Route path="/*" element={<Dashboard />} />
      <Route path="/profile" element={<Profile />} />
      <Route path="/dashboard" element={<Dashboard />} />
    </Routes>
  );
};

export default HomePage;
