import { login } from "../api/octockupApi";

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
