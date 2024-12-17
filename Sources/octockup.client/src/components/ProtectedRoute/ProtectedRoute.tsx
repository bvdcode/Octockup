import React, { useEffect } from "react";
import { checkAuth } from "../../api/api";
import { Navigate, useNavigate } from "react-router-dom";
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
  const signOut = useSignOut();
  const navigate = useNavigate();
  const authHeader = useAuthHeader();
  const isAuthenticated = useIsAuthenticated();

  useEffect(() => {
    const handleLogout = () => {
      signOut();
      navigate("/login");
    };
    if (isAuthenticated && authHeader) {
      AxiosClient.setAuthHeader(authHeader);
      checkAuth();
    }
    AxiosClient.events.on("logout", handleLogout);
    return () => {
      AxiosClient.events.off("logout", handleLogout);
    };
  }, [authHeader, isAuthenticated, navigate, signOut]);

  return isAuthenticated ? children : <Navigate to={fallbackPath} />;
};

export default ProtectedRoute;
