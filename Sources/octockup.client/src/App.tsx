import "./App.css";
import IUserData from "./auth/IUserData";
import AuthProvider from "react-auth-kit";
import { HomePage, LoginPage } from "./pages";
import { ToastContainer } from "react-toastify";
import { refresh } from "./auth/AuthKitMethods";
import createStore from "react-auth-kit/createStore";
import AuthOutlet from "@auth-kit/react-router/AuthOutlet";
import { Route, Routes, BrowserRouter } from "react-router-dom";

const store = createStore<IUserData>({
  authName: "OCTOCKUP_AUTH",
  authType: "localstorage",
  refresh: refresh,
});

function App() {
  return (
    <AuthProvider store={store}>
      <ToastContainer />
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<AuthOutlet fallbackPath="/login" />}>
            <Route path="/*" element={<HomePage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App;
