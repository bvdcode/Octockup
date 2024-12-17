import "./index.css";
import {
  BrowserRouter as Router,
  Route,
  Routes,
  Navigate,
} from "react-router-dom";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { HomePage, LoginPage } from "./pages";

const isAuthenticated = () => {
  return localStorage.getItem("authToken") !== null;
};

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <Router>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/*"
          element={isAuthenticated() ? <HomePage /> : <Navigate to="/login" />}
        />
      </Routes>
    </Router>
  </StrictMode>
);