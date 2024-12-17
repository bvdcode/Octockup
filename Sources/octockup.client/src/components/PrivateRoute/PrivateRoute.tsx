import { Navigate } from "react-router-dom";
import React, { useEffect, useState } from "react";
import { isAuthenticated } from "../../services/authService";

interface PrivateRouteProps {
  children: JSX.Element;
}

const PrivateRoute: React.FC<PrivateRouteProps> = ({ children }) => {
  const [auth, setAuth] = useState<boolean | null>(null);

  useEffect(() => {
    const checkAuthStatus = async () => {
      const authStatus = await isAuthenticated();
      setAuth(authStatus);
    };

    checkAuthStatus();
  }, []);

  if (auth === null) {
    return <div>Loading...</div>;
  }

  return auth ? children : <Navigate to="/login" />;
};

export default PrivateRoute;
