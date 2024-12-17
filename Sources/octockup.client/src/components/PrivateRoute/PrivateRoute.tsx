import { Navigate } from "react-router-dom";

interface PrivateRouteProps {
  element: JSX.Element;
}

const PrivateRoute: React.FC<PrivateRouteProps> = ({ element }) => {
  const isAuthenticated = localStorage.getItem("authToken") !== null;
  return isAuthenticated ? element : <Navigate to="/login" />;
};

export default PrivateRoute;
