export interface LoginRequest {
  username: string;
  passwordHash: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
}
