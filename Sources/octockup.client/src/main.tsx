import "./index.css";
import { StrictMode } from "react";
import { PrivateRoute } from "./components";
import { createRoot } from "react-dom/client";
import { HomePage, LoginPage } from "./pages";
import { Route, Routes, BrowserRouter as Router } from "react-router-dom";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <Router>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/*" element={<PrivateRoute element={<HomePage />} />} />
      </Routes>
    </Router>
  </StrictMode>
);
