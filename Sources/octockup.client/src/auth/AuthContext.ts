import { createContext } from "react";
import { SignInProps } from "./types";

interface AuthContextProps {
  isAuthenticated: boolean;
  accessToken: string | null;
  userState: unknown | null;
  isLoaded: boolean;
  signOut: () => void;
  signIn: (data: SignInProps) => void;
}

const AuthContext = createContext<AuthContextProps | undefined>(undefined);

export default AuthContext;
