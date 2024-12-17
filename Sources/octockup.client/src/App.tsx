import "./App.css";
import AuthProvider from "react-auth-kit";
import { HomePage, LoginPage } from "./pages";
import createStore from "react-auth-kit/createStore";
import { Route, Routes, BrowserRouter as Router } from "react-router-dom";
import createRefresh from "react-auth-kit/createRefresh";

const refresh = createRefresh({
  refreshApiCallback: async (token) => {
    // mock
    console.log("Refresh API Called", token);
    return {
      isSuccess: true,
      newAuthToken: "newAuthToken",
      newAuthTokenExpireIn: 10,
      newRefreshTokenExpiresIn: 60,
    };
  },
  interval: 60,
});

const store = createStore({
  authName: "OCTOCKUP",
  authType: "localstorage",
  refresh: refresh, // createRefreshParamInterface
});

function App() {
  return (
    <Router>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/*"
          element={
            <AuthProvider store={store}>
              <HomePage />
            </AuthProvider>
          }
        />
      </Routes>
    </Router>
  );
}

export default App;
