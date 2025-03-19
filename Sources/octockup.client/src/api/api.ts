import {
  LoginRequest,
  AuthResponse,
  BackupTask,
  BackupProvider,
  CreateJobRequest,
  DataPage,
  BackupSnapshot,
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
export const getBackups = async (
  page: number = 1,
  pageSize: number = 20,
  orderByDesc: boolean = true
): Promise<DataPage<BackupTask>> => {
  const orderDirection = orderByDesc ? "desc" : "asc";
  const url = `/backups/list?orderBy=id ${orderDirection}&page=${page}&pageSize=${pageSize}`;
  const response = await AxiosClient.getInstance().get<BackupTask[]>(url);
  response.data.forEach((backup) => {
    if (backup.completedAt) {
      backup.completedAtDate = new Date(backup.completedAt);
    }
    backup.elapsed = backup.elapsed.split(".")[0];
    if (backup.interval === "00:00:00") {
      backup.interval = "-";
    }
  });
  return {
    data: response.data,
    totalCount: response.headers["x-total-count"],
  };
};

/**
 * Fetches the list of backup providers from the server.
 *
 * @returns {Promise<BackupProvider[]>} A promise that resolves to an array of BackupProvider objects.
 */
export const getProviders = async (): Promise<BackupProvider[]> => {
  const response = await AxiosClient.getInstance().get<BackupProvider[]>(
    "/backups/providers"
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
  await AxiosClient.getInstance().post("/backups/create", job);
};

/**
 * Triggers a backup job to run immediately.
 *
 * @param id - The unique identifier of the backup job to be triggered.
 * @returns A promise that resolves when the job has been successfully triggered.
 */
export const forceRunJob = async (id: number): Promise<void> => {
  await AxiosClient.getInstance().patch(`/backups/${id}/start`);
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
  await AxiosClient.getInstance().patch(`/backups/${id}/stop`);
};

/**
 * Deletes a job with the specified ID.
 *
 * @param id - The ID of the job to delete.
 * @returns A promise that resolves when the job is deleted.
 */
export const deleteJob = async (id: number): Promise<void> => {
  await AxiosClient.getInstance().delete(`/backups/${id}`);
};

/**
 * Fetches a paginated list of backup snapshots for a given backup task.
 *
 * @param backupId - The ID of the backup task.
 * @param page - The page number to retrieve.
 * @param pageSize - The number of items per page.
 * @param orderBy - The order in which to sort the snapshots by ID. Can be "asc" for ascending or "desc" for descending. Defaults to "desc".
 * @returns A promise that resolves to a DataPage containing an array of BackupSnapshot objects and the total count of snapshots.
 */
export const getSnapshots = async (
  backupId: number,
  page: number,
  pageSize: number,
  orderBy: "asc" | "desc" = "desc"
): Promise<DataPage<BackupSnapshot>> => {
  const url = `/snapshots?page=${page}&pageSize=${pageSize}&orderBy=id ${orderBy}&filter=BackupTaskId=${backupId}`;
  const response = await AxiosClient.getInstance().get<BackupSnapshot[]>(url);
  response.data.forEach((snapshot) => {
    snapshot.createdAtDate = new Date(snapshot.createdAt);
    snapshot.updatedAtDate = new Date(snapshot.updatedAt);
  });
  return {
    data: response.data,
    totalCount: response.headers["x-total-count"],
  };
};

export const deleteSnapshot = async (snapshotId: number): Promise<void> => {
  await AxiosClient.getInstance().delete(`/snapshots/${snapshotId}`);
};

export const triggerJob = async (): Promise<void> => {
  await AxiosClient.getInstance().patch("/backups/trigger-job");
};
