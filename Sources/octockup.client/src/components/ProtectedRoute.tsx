import Loader from "./Loader";
import { checkAuth } from "../api/api";
import React, { useEffect } from "react";
import AxiosClient from "../api/AxiosClient";
import { Navigate, useNavigate } from "react-router-dom";
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
  const [isHeaderSet, setIsHeaderSet] = React.useState(false);

  useEffect(() => {
    const handleLogout = () => {
      signOut();
      navigate("/login");
    };
    if (isAuthenticated && authHeader) {
      AxiosClient.setAuthHeader(authHeader);
      setIsHeaderSet(true);
    }
    checkAuth();
    AxiosClient.events.on("logout", handleLogout);
    return () => {
      AxiosClient.events.off("logout", handleLogout);
    };
  }, [authHeader, isAuthenticated, isHeaderSet, navigate, signOut]);

  return isAuthenticated && authHeader ? (
    isHeaderSet ? (
      children
    ) : (
      <Loader />
    )
  ) : (
    <Navigate to={fallbackPath} />
  );
};

export default ProtectedRoute;
