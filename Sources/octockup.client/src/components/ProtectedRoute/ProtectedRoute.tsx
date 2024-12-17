import React, { useEffect } from "react";
import { Navigate } from "react-router-dom";
import AxiosClient from "../../api/AxiosClient";
import useAuthHeader from "react-auth-kit/hooks/useAuthHeader";
import useIsAuthenticated from "react-auth-kit/hooks/useIsAuthenticated";

interface ProtectedRouteProps {
  children: JSX.Element;
  fallbackPath: string;
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({
  children,
  fallbackPath,
}) => {
  const authHeader = useAuthHeader();
  const isAuthenticated = useIsAuthenticated();

  useEffect(() => {
    AxiosClient.setAuthHeader(authHeader);
    console.log("authHeader :>> ", authHeader);
    console.log("isAuthenticated :>> ", isAuthenticated);
  }, [authHeader, isAuthenticated]);

  return isAuthenticated ? children : <Navigate to={fallbackPath} />;
};

export default ProtectedRoute;
