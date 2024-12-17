import "./App.css";
import { useEffect } from "react";
import { HomePage, LoginPage } from "./pages";
import { refreshToken } from "./services/authService";
import PrivateRoute from "./components/PrivateRoute/PrivateRoute";
import { Route, Routes, BrowserRouter as Router } from "react-router-dom";

function App() {
  useEffect(() => {
    const refresh = async () => {
      await refreshToken();
    };

    refresh();
  }, []);

  return (
    <Router>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/*"
          element={
            <PrivateRoute>
              <HomePage />
            </PrivateRoute>
          }
        />
      </Routes>
    </Router>
  );
}

export default App;
