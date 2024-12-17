import { login } from "../api/octockupApi";

export const isAuthenticated = () => {
  return localStorage.getItem("authToken") !== null;
};

export const handleLogin = async (username: string, password: string) => {
  try {
    const data = await login(username, password);
    localStorage.setItem("authToken", data.accessToken);
    return true;
  } catch (error) {
    console.error("Login error:", error);
    return false;
  }
};
