export enum ModuleDestination { Source = 1, Target = 2 }

// Match backend JSON (C# property names are PascalCase)
export interface Module {
  id: string;
  createdAt: string;
  userId: string;
  tag: string;
  backupModuleId: string;
  destination: ModuleDestination;
}

export interface ModuleProviderInfo {
  id: string;
  pathSeparator: string;
  name: string;
  requiredParameters: string[];
}

export interface TestResultItem {
  path: string;
  name: string;
  size: number;
  lastModified?: string;
}

export interface CreateModuleRequest {
  destination: ModuleDestination;
  tag: string;
  backupModuleId: string;
  parameters: Record<string, string>;
}
export interface BackupItem {
  id: string;
  tag: string;
  sourceId: string;
  storageId: string;
  ignoredPaths: string[];
  source: Module;
  storage: Module;
}

export interface CreateBackupRequest {
  sourceId: string;
  storageId: string;
  tag: string;
  ignoredPaths: string[];
}

export enum BackupStatus {
  Created = 0,
  Running = 1,
  Failed = 2,
  Completed = 3,
}

export interface ScheduleItem {
  id: string;
  backupId: string;
  startAt: string;
  interval?: string | null;
  status: BackupStatus;
  finishedAt?: string | null;
  errorMessage?: string | null;
  backupTag: string;
  sourceTag: string;
  storageTag: string;
  sourceProviderId: string;
  storageProviderId: string;
}

export interface CreateScheduleRequest {
  backupId: string;
  startAt: string;
  intervalMinutes?: number;
}
