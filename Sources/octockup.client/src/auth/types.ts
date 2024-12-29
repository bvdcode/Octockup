export interface AuthProviderProps {
  storageKey: string;
  refreshIntervalSeconds: number;
  refresh: (refreshToken: string) => Promise<{
    newUserState: unknown;
    newAccessToken: string;
    newRefreshToken: string;
  }>;
  children: React.ReactNode;
}

export interface AuthContextProps {
  isAuthenticated: boolean;
  accessToken: string | null;
  signOut: () => void;
  signIn: (data: SignInProps) => void;
}

export interface SignInProps {
  auth: {
    token: string;
  };
  refresh: string;
  userState: unknown;
}
