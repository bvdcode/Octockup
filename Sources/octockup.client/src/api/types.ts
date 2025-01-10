export interface BaseResponse {
  id: number;
  createdAt: string;
  updatedAt: string;
  createdAtDate: Date;
  updatedAtDate: Date;
}

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
  elapsed: string;
  interval: string;
  progress: number;
  completedAt: string;
  status: BackupTaskStatus;
  lastMessage: string | null;
  completedAtDate: Date | null;
}

export enum BackupTaskStatus {
  Created = 0,
  Running = 1,
  Failed = 2,
  Completed = 3,
}

export interface BackupProvider {
  name: string;
  class: string;
  parameters: string[];
}

export interface CreateJobRequest {
  name: string;
  provider: string;
  interval: number;
  startAt: Date | null;
  isNotificationEnabled: boolean;
  parameters: Record<string, string>;
}

export interface DataPage<T> {
  data: T[];
  totalCount: number;
}

export interface BackupSnapshot extends BaseResponse {
  log: string;
  elapsed: string;
  totalSize: number;
  fileCount: number;
  backupTaskId: number;
  totalSizeFormatted: string;
}
