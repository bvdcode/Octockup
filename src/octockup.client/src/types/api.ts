export enum ModuleDestination {
  Source = 1,
  Target = 2,
}

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

export type BackupSource = ModuleProviderInfo;
export type BackupStorage = ModuleProviderInfo;

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
  disableCompression: boolean;
  disableEncryption: boolean;
  source: Module;
  storage: Module;
  schedules: ScheduleItem[];
  snapshots: SnapshotDto[];
  createdAt?: string;
  updatedAt?: string;
}

export interface BackupDeletionResult {
  deleted: boolean;
  errorMessage?: string | null;
  deletedSchedules: number;
  deletedSnapshots: number;
  deletedSnapshotFiles: number;
}

export interface ScheduleBackupItem {
  id: string;
  tag: string;
  sourceId: string;
  storageId: string;
  ignoredPaths: string[];
  disableCompression: boolean;
  disableEncryption: boolean;
  source: Module;
  storage: Module;
  snapshots: SnapshotDto[];
  createdAt?: string;
  updatedAt?: string;
}

export interface SnapshotDto {
  id: string;
  backupId: string;
  completedAt?: string | null;
  filesCount: number;
  totalSize: number;
}

export interface CreateBackupRequest {
  sourceId: string;
  storageId: string;
  tag: string;
  ignoredPaths: string[];
  disableCompression: boolean;
  disableEncryption: boolean;
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
  backup: ScheduleBackupItem;
}

export interface CreateScheduleRequest {
  backupId: string;
  startAt: string;
  intervalMinutes?: number;
}

export interface ScheduleReport {
  scheduleId: string;
  backupId: string;
  timestamp: string;
  status: BackupStatus;
  message: string;
  total: number;
  processed: number;
  speed: number;
  elapsed?: string;
  isEnumerationCompleted: boolean;
  currentPath: string;
  currentFile: string;
}

export interface SnapshotFileDto {
  id: string;
  size: number;
  snapshotId: string;
  lastModified?: string | null;
  name: string;
  path: string;
  hashsum: string;
}

export enum StorageCleanupStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Canceled = 4,
}

export enum StorageCleanupPhase {
  Preparing = 0,
  CollectingReferences = 1,
  ScanningStorage = 2,
  Completed = 3,
}

export interface StorageCleanupJob {
  jobId: string;
  userId: string;
  storageId: string;
  storageTag: string;
  status: StorageCleanupStatus;
  phase: StorageCleanupPhase;
  startedAt: string;
  finishedAt?: string | null;
  errorMessage?: string | null;
  snapshotFilesScanned: number;
  referenceCount: number;
  referencedChunks: number;
  storageObjectsScanned: number;
  storageBytesScanned: number;
  chunkObjectsScanned: number;
  referencedObjects: number;
  referencedBytes: number;
  orphanObjects: number;
  orphanBytes: number;
  deletedObjects: number;
  freedBytes: number;
  missingObjects: number;
  failedDeletes: number;
  skippedObjects: number;
  uploadedHashRowsDeleted: number;
  currentPath?: string | null;
}

export interface StorageMaintenanceSummary extends Module {
  totalBackups?: number | null;
  indexedObjects?: number | null;
  indexedStoredSize?: number | null;
  indexedOriginalSize?: number | null;
  referenceCount?: number | null;
  referencedChunks?: number | null;
  deduplicatedChunks?: number | null;
  totalCapacityBytes?: number | null;
  availableBytes?: number | null;
  activeJob?: StorageCleanupJob | null;
  lastJob?: StorageCleanupJob | null;
}
