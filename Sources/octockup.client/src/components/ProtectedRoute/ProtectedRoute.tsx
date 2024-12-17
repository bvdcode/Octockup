import React, { useEffect } from "react";
import { checkAuth } from "../../api/api";
import { Navigate } from "react-router-dom";
import AxiosClient from "../../api/AxiosClient";
import useSignOut from "react-auth-kit/hooks/useSignOut";
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
  const signOut = useSignOut();

  useEffect(() => {
    const handleLogout = () => {
      signOut();
    };
    AxiosClient.setAuthHeader(authHeader);
    AxiosClient.events.on("logout", handleLogout);
    checkAuth();
    return () => {
      AxiosClient.events.off("logout", handleLogout);
    };
  }, [authHeader, isAuthenticated, signOut]);

  return isAuthenticated ? children : <Navigate to={fallbackPath} />;
};

export default ProtectedRoute;
