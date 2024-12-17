import "./App.css";
import AuthProvider from "react-auth-kit";
import { HomePage, LoginPage } from "./pages";
import { ToastContainer } from "react-toastify";
import { refresh } from "./auth/AuthKitMethods";
import createStore from "react-auth-kit/createStore";
import { Route, Routes, BrowserRouter as Router } from "react-router-dom";

const store = createStore({
  authName: "OCTOCKUP",
  authType: "localstorage",
  refresh: refresh,
});

function App() {
  return (
    <Router>
      <ToastContainer />
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
