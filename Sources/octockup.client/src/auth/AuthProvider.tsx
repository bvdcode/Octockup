// filepath: src/providers/AuthProvider.tsx
import React, { useCallback, useEffect, useState } from "react";
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
  const [isLoaded, setIsLoaded] = useState(false);

  const storageUserKey = `${storageKey}_user`;
  const storageRefreshKey = `${storageKey}_refresh`;

  const signOut = useCallback(() => {
    localStorage.removeItem(storageUserKey);
    localStorage.removeItem(storageRefreshKey);
    setAccessToken(null);
    setUserState(null);
    setIsAuthenticated(false);
    setIsLoaded(true);
  }, [storageRefreshKey, storageUserKey]);

  const signIn = useCallback(
    (data: SignInProps) => {
      localStorage.setItem(storageUserKey, JSON.stringify(data.userState));
      localStorage.setItem(storageRefreshKey, data.refresh);
      setAccessToken(data.auth.token);
      setUserState(data.userState);
      setIsAuthenticated(true);
      setIsLoaded(true);
    },
    [storageRefreshKey, storageUserKey]
  );

  const doRefreshToken = useCallback(async () => {
    const refreshToken = localStorage.getItem(storageRefreshKey);
    if (!refreshToken) {
      setIsLoaded(true);
      return;
    }
    await refresh(refreshToken || "")
      .then((result) => {
        setUserState(result.newUserState);
        setAccessToken(result.newAccessToken);
        localStorage.setItem(
          storageUserKey,
          JSON.stringify(result.newUserState)
        );
        localStorage.setItem(storageRefreshKey, result.newRefreshToken);
        setIsAuthenticated(true);
        setIsLoaded(true);
      })
      .catch((e) => {
        if (e.response?.status === 404) {
          setIsLoaded(true);
        }
      });
  }, [refresh, storageRefreshKey, storageUserKey]);

  useEffect(() => {
    doRefreshToken();
  }, []);

  useEffect(() => {
    const decodeTokenRemainingTime = (token: string) => {
      const defaultInterval = 60 * 1000;
      try {
        const [, payload] = token.split(".");
        const decodedPayload = JSON.parse(atob(payload));
        const remainingTime = decodedPayload.exp * 1000 - Date.now();
        return remainingTime;
      } catch {
        return defaultInterval;
      }
    };

    const intervalMs =
      refreshIntervalSeconds === "auto"
        ? decodeTokenRemainingTime(accessToken || "")
        : refreshIntervalSeconds * 1000;

    const interval = setInterval(async () => {
      await doRefreshToken();
    }, intervalMs);

    return () => clearInterval(interval);
  }, [refreshIntervalSeconds, refresh, doRefreshToken, accessToken]);

  return (
    <AuthContext.Provider
      value={{
        isAuthenticated,
        accessToken,
        userState,
        isLoaded,
        signOut,
        signIn,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export default AuthProvider;
