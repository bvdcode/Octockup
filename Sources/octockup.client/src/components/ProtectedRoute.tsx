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
  const { signOut, isAuthenticated, accessToken, isLoaded } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (isLoaded && isAuthenticated && accessToken) {
      AxiosClient.setAuthHeader(accessToken);
      checkAuth();
    }
  }, [accessToken, isAuthenticated, isLoaded]);

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

  console.log(isLoaded, isAuthenticated, accessToken !== null);

  return isLoaded ? (
    isAuthenticated && accessToken ? (
      children
    ) : (
      <Navigate to={fallbackPath} />
    )
  ) : (
    <Loader />
  );
};

export default ProtectedRoute;
