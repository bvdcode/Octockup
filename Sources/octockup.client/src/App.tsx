import "./App.css";
import { HomePage, LoginPage } from "./pages";
import PrivateRoute from "./components/PrivateRoute/PrivateRoute";
import { Route, Routes, BrowserRouter as Router } from "react-router-dom";

function App() {
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
