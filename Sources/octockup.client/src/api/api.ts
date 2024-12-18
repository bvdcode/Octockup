import SHA512 from "crypto-js/sha512";
import AxiosClient from "./AxiosClient";
import { LoginRequest, AuthResponse } from "./types";

/**
 * Logs in a user with the provided username and password.
 *
 * @param username - The username of the user.
 * @param password - The password of the user.
 * @returns A promise that resolves to a `LoginResponse` object if the login is successful.
 * @throws An error if the login fails.
 */
export const login = async (
  username: string,
  password: string
): Promise<AuthResponse> => {
  const request = {
    username: username,
    passwordHash: SHA512(password).toString(),
  } as LoginRequest;
  const response = await AxiosClient.getInstance().post<AuthResponse>(
    "/auth/login",
    request
  );
  return response.data;
};

/**
 * Refreshes the access token using the provided refresh token.
 *
 * @param refreshToken - The refresh token to use.
 * @returns A promise that resolves to a `LoginResponse` object if the refresh is successful.
 * @throws An error if the refresh fails.
 */
export const refreshAccessToken = async (
  refreshToken: string
): Promise<AuthResponse> => {
  const response = await AxiosClient.getInstance().post<AuthResponse>(
    "/auth/refresh",
    { refreshToken }
  );
  return response.data;
};

/**
 * Checks if the current user is authenticated.
 *
 * @returns A promise that resolves to `true` if the user is authenticated, and `false` otherwise.
 */
export const checkAuth = async (): Promise<boolean> => {
  const response = await AxiosClient.getInstance().get("/auth/check");
  return response.status === 200;
};

export const changePassword = async (newPassword: string): Promise<void> => {
  await AxiosClient.getInstance().post("/auth/password", {
    newPassword: newPassword,
  });
};
