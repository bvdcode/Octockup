export interface LoginRequest {
  username: string;
  passwordHash: string;
}

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
}
