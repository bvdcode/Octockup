import SHA512 from "crypto-js/sha512";
import AxiosClient from "./AxiosClient";
import {
  LoginRequest,
  AuthResponse,
  BackupTask,
  BackupProvider,
  CreateJobRequest,
} from "./types";

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
    "/auth/refresh-token",
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
  const response = await AxiosClient.getInstance().get("/auth/check-token");
  return response.status === 200;
};

export const changePassword = async (newPassword: string): Promise<void> => {
  await AxiosClient.getInstance().post("/auth/change-password", {
    newPassword: newPassword,
  });
};

export const getBackupStatus = async (): Promise<BackupTask[]> => {
  const response = await AxiosClient.getInstance().get<BackupTask[]>(
    "/backup/list?orderBy=createdAt desc"
  );
  response.data.forEach((backup) => {
    if (backup.completedAt) {
      backup.completedAtDate = new Date(backup.completedAt);
    }
    backup.elapsed = backup.elapsed.split(".")[0];
    if (backup.interval === "00:00:00") {
      backup.interval = "-";
    }
  });
  return response.data;
};

export const getProviders = async (): Promise<BackupProvider[]> => {
  const response = await AxiosClient.getInstance().get<BackupProvider[]>(
    "/backup/providers"
  );
  return response.data;
};

export const createBackupJob = async (job: CreateJobRequest): Promise<void> => {
  await AxiosClient.getInstance().post("/backup/create", job);
};

export const forceRunJob = async (id: number): Promise<void> => {
  await AxiosClient.getInstance().patch(`/backup/${id}/trigger`);
};
