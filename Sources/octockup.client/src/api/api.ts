import {
  LoginRequest,
  AuthResponse,
  BackupTask,
  BackupProvider,
  CreateJobRequest,
} from "./types";
import SHA512 from "crypto-js/sha512";
import AxiosClient from "./AxiosClient";

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

/**
 * Changes the user's password.
 *
 * @param newPassword - The new password to be set for the user.
 * @returns A promise that resolves when the password has been successfully changed.
 */
export const changePassword = async (newPassword: string): Promise<void> => {
  await AxiosClient.getInstance().post("/auth/change-password", {
    newPassword: newPassword,
  });
};

/**
 * Fetches the backup status from the server.
 *
 * This function sends a GET request to the "/backup/list" endpoint with the query parameter
 * `orderBy=createdAt desc` to retrieve a list of backup tasks ordered by their creation date
 * in descending order. It processes the response data to:
 * - Convert the `completedAt` timestamp to a `Date` object and assign it to `completedAtDate`.
 * - Remove the milliseconds part from the `elapsed` time string.
 * - Replace the `interval` value of "00:00:00" with a dash ("-").
 *
 * @returns {Promise<BackupTask[]>} A promise that resolves to an array of `BackupTask` objects.
 */
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

/**
 * Fetches the list of backup providers from the server.
 *
 * @returns {Promise<BackupProvider[]>} A promise that resolves to an array of BackupProvider objects.
 */
export const getProviders = async (): Promise<BackupProvider[]> => {
  const response = await AxiosClient.getInstance().get<BackupProvider[]>(
    "/backup/providers"
  );
  return response.data;
};

/**
 * Creates a backup job by sending a POST request to the "/backup/create" endpoint.
 *
 * @param job - The job request object containing the details of the backup job to be created.
 * @returns A promise that resolves when the backup job is successfully created.
 */
export const createBackupJob = async (job: CreateJobRequest): Promise<void> => {
  await AxiosClient.getInstance().post("/backup/create", job);
};

/**
 * Triggers a backup job to run immediately.
 *
 * @param id - The unique identifier of the backup job to be triggered.
 * @returns A promise that resolves when the job has been successfully triggered.
 */
export const forceRunJob = async (id: number): Promise<void> => {
  await AxiosClient.getInstance().patch(`/backup/${id}/start`);
};

/**
 * Stops a job with the given ID.
 *
 * This function sends a PATCH request to the server to stop the job
 * identified by the provided ID.
 *
 * @param {number} id - The ID of the job to stop.
 * @returns {Promise<void>} A promise that resolves when the job is stopped.
 */
export const stopJob = async (id: number): Promise<void> => {
  await AxiosClient.getInstance().patch(`/backup/${id}/stop`);
};

/**
 * Deletes a job with the specified ID.
 *
 * @param id - The ID of the job to delete.
 * @returns A promise that resolves when the job is deleted.
 */
export const deleteJob = async (id: number): Promise<void> => {
  await AxiosClient.getInstance().delete(`/backup/${id}`);
};
