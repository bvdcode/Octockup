import ApiClient from "../api/apiClient";
import { LOCAL_STORAGE_REFRESH_TOKEN_KEY } from "../config";
import { login, refreshAccessToken, checkAuth } from "../api/octockupApi";

export const isAuthenticated = async (): Promise<boolean> => {
  const refreshToken = localStorage.getItem(LOCAL_STORAGE_REFRESH_TOKEN_KEY);
  if (!refreshToken) {
    return false;
  }
  return checkAuth();
};

export const handleLogin = async (
  username: string,
  password: string
): Promise<boolean> => {
  try {
    const data = await login(username, password);
    localStorage.setItem(LOCAL_STORAGE_REFRESH_TOKEN_KEY, data.refreshToken);
    ApiClient.setAccessToken(data.accessToken);
    return true;
  } catch (error) {
    console.error("Login error:", error);
    return false;
  }
};

export const refreshToken = async (): Promise<boolean> => {
  const refreshToken = localStorage.getItem(LOCAL_STORAGE_REFRESH_TOKEN_KEY);
  if (!refreshToken) {
    return false;
  }

  try {
    const data = await refreshAccessToken(refreshToken);
    localStorage.setItem(LOCAL_STORAGE_REFRESH_TOKEN_KEY, data.refreshToken);
    ApiClient.setAccessToken(data.accessToken);
    return true;
  } catch (error) {
    console.error("Refresh token error:", error);
    return false;
  }
};
