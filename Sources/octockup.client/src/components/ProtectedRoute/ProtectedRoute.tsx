import React, { useEffect } from "react";
import { Navigate } from "react-router-dom";
import useAuthHeader from "react-auth-kit/hooks/useAuthHeader";
import useIsAuthenticated from "react-auth-kit/hooks/useIsAuthenticated";
import AxiosClient from "../../api/AxiosClient";

interface ProtectedRouteProps {
  children: JSX.Element;
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children }) => {
  const authHeader = useAuthHeader();
  const isAuthenticated = useIsAuthenticated();

  useEffect(() => {
    AxiosClient.setAuthHeader(authHeader);
    console.log("authHeader :>> ", authHeader);
    console.log("isAuthenticated :>> ", isAuthenticated);
  }, [authHeader, isAuthenticated]);

  return isAuthenticated ? children : <Navigate to="/login" />;
};

export default ProtectedRoute;
