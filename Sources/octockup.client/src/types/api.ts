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

export enum TaskStatus {
  Created = 0,
  Running = 1,
  Failed = 2,
  Completed = 3,
}

export interface BackupSummary {
  id: string;
  tag: string;
  sourceId: string;
  storageId: string;
  sourceTag: string;
  storageTag: string;
  sourceProviderId: string;
  storageProviderId: string;
}

export interface TaskItem {
  id: string;
  backupId: string;
  startAt: string;
  interval?: string | null;
  status: TaskStatus;
  finishedAt?: string | null;
  errorMessage?: string | null;
  backupTag: string;
  sourceTag: string;
  storageTag: string;
  sourceProviderId: string;
  storageProviderId: string;
}

export interface CreateTaskRequest {
  backupId: string;
  startAt: string;
  intervalMinutes?: number;
}
