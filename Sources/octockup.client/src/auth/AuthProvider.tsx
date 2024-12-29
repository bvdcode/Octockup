// filepath: src/providers/AuthProvider.tsx
import React, { useEffect, useState } from "react";
import AuthContext from "./AuthContext";
import { AuthProviderProps, SignInProps } from "./types";

const AuthProvider: React.FC<AuthProviderProps> = ({
  storageKey,
  refreshIntervalSeconds,
  refresh,
  children,
}) => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [userState, setUserState] = useState<unknown | null>(null);

  const storageUserKey = `${storageKey}_user`;
  const storageRefreshKey = `${storageKey}_refresh`;

  const signOut = () => {
    localStorage.removeItem(storageUserKey);
    localStorage.removeItem(storageRefreshKey);
    setAccessToken(null);
    setUserState(null);
    setIsAuthenticated(false);
  };

  const signIn = (data: SignInProps) => {
    localStorage.setItem(storageUserKey, JSON.stringify(data.userState));
    localStorage.setItem(storageRefreshKey, data.refresh);
    setAccessToken(data.auth.token);
    setUserState(data.userState);
    setIsAuthenticated(true);
  };

  useEffect(() => {
    const interval = setInterval(async () => {
      const refreshToken = localStorage.getItem(storageRefreshKey);
      if (!refreshToken) {
        return;
      }
      await refresh(refreshToken || "").then((result) => {
        setUserState(result.newUserState);
        setAccessToken(result.newAccessToken);
        setIsAuthenticated(true);
        localStorage.setItem(
          storageUserKey,
          JSON.stringify(result.newUserState)
        );
        localStorage.setItem(storageRefreshKey, result.newRefreshToken);
      });
    }, refreshIntervalSeconds * 1000);

    return () => clearInterval(interval);
  }, [refreshIntervalSeconds, refresh, storageRefreshKey, storageUserKey]);

  return (
    <AuthContext.Provider
      value={{ isAuthenticated, accessToken, userState, signOut, signIn }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export default AuthProvider;
