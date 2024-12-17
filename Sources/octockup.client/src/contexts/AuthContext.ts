import ApiClient from "../api/apiClient";
import { getTokens } from "../api/octockupApi";
import { LOCAL_STORAGE_REFRESH_TOKEN_KEY } from "../config";
import { createContext, useContext, useState, ReactNode } from "react";

interface AuthContextProps {
  isAuth: boolean;
  login: (username: string, password: string) => Promise<boolean>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextProps | undefined>(undefined);

export const useAuth = (): AuthContextProps => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
};

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider = ({ children }: AuthProviderProps) => {
  const [isAuth, setIsAuth] = useState(false);

  const login = async (
    username: string,
    password: string
  ): Promise<boolean> => {
    const tokens = await getTokens(username, password);
    if (!tokens.accessToken) {
      return false;
    }
    localStorage.setItem(LOCAL_STORAGE_REFRESH_TOKEN_KEY, tokens.refreshToken);
    ApiClient.setAccessToken(tokens.accessToken);
    setIsAuth(true);
    return true;
  };

  const logout = () => {
    localStorage.setItem(LOCAL_STORAGE_REFRESH_TOKEN_KEY, "");
    ApiClient.setAccessToken("");
    setIsAuth(false);
  };

  return (
    <AuthContext.Provider value={{ isAuth, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};
