import { ProtectedRoute } from "./components";
import { HomePage, LoginPage } from "./pages";
import { BrowserRouter, Route, Routes } from "react-router-dom";

const RoutesComponent = () => {
  return (
    <BrowserRouter>
      <Routes>
        <Route path={"/login"} element={<LoginPage />} />
        <Route
          path={"/*"}
          element={
            <ProtectedRoute fallbackPath={"/login"}>
              <HomePage />
            </ProtectedRoute>
          }
        />
      </Routes>
    </BrowserRouter>
  );
};

export default RoutesComponent;
