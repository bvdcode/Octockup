import Loader from "./Loader";
import useAuth from "../auth/useAuth";
import { checkAuth } from "../api/api";
import React, { useEffect } from "react";
import AxiosClient from "../api/AxiosClient";
import { Navigate, useNavigate } from "react-router-dom";

interface ProtectedRouteProps {
  children: JSX.Element;
  fallbackPath: string;
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({
  children,
  fallbackPath,
}) => {
  const { signOut, isAuthenticated, accessToken } = useAuth();
  const navigate = useNavigate();
  const [isHeaderSet, setIsHeaderSet] = React.useState(false);

  useEffect(() => {
    if (isAuthenticated && accessToken) {
      AxiosClient.setAuthHeader(accessToken);
      setIsHeaderSet(true);
    }
    checkAuth();
  }, [accessToken, isAuthenticated, isHeaderSet]);

  useEffect(() => {
    const handleLogout = () => {
      signOut();
      navigate("/login");
    };
    AxiosClient.events.on("logout", handleLogout);
    return () => {
      AxiosClient.events.off("logout", handleLogout);
    };
  }, [navigate, signOut]);

  return isAuthenticated && accessToken ? (
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
