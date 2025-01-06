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
  const [isTokenSet, setIsTokenSet] = React.useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    if (accessToken) {
      AxiosClient.setAuthHeader(accessToken);
      setIsTokenSet(true);
    } else {
      if (isLoaded) {
        setIsTokenSet(true);
      }
    }
    if (isLoaded && isAuthenticated && accessToken) {
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

  return isLoaded && isTokenSet ? (
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
