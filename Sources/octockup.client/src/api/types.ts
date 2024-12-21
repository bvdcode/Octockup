export interface LoginRequest {
  username: string;
  passwordHash: string;
}

export interface AuthResponse {
  user: User;
  accessToken: string;
  refreshToken: string;
}

export interface User {
  id: number;
  username: string;
  email: string;
  role: UserRole;
}

export enum UserRole {
  Blocked = 0,
  Regular = 1,
  Admin = 2,
}

export interface BackupTask {
  id: number;
  name: string;
  lastRun: string;
  duration: string;
  progress: number;
  lastRunDate: Date;
  status: BackupTaskStatus;
}

export enum BackupTaskStatus {
  Created = 0,
  Running = 1,
  Failed = 2,
  Completed = 3,
}

export interface BackupProvider {
  name: string;
  parameters: string[];
}

export interface CreateJobRequest {
  provider: string;
  settings: Record<string, string>;
  name: string;
  interval: number;
  notifications: boolean;
  startAt: Date | null;
}
