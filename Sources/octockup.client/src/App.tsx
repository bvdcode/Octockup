import "./App.css";
import RoutesComponent from "./Routes";
import IUserData from "./auth/IUserData";
import AuthProvider from "react-auth-kit";
import { refresh } from "./auth/AuthKitMethods";
import { ToastContainer } from "react-toastify";
import createStore from "react-auth-kit/createStore";

function App() {
  const store = createStore<IUserData>({
    authName: "_octockup_auth",
    authType: "localstorage",
    refresh: refresh,
  });

  return (
    <AuthProvider store={store}>
      <ToastContainer />
      <RoutesComponent />
    </AuthProvider>
  );
}

export default App;
