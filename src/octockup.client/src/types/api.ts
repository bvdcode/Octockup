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
  snapshotCount: number;
  completedSnapshotCount: number;
  scheduleCount: number;
  latestSnapshot?: SnapshotDto | null;
  activeSchedule?: BackupScheduleItem | null;
  latestFinishedSchedule?: BackupScheduleItem | null;
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

export enum SnapshotArchiveStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Canceled = 4,
}

export enum SnapshotArchivePhase {
  Waiting = 0,
  Preparing = 1,
  Streaming = 2,
}

export interface SnapshotArchiveJob {
  jobId: string;
  userId: string;
  snapshotId: string;
  status: SnapshotArchiveStatus;
  phase: SnapshotArchivePhase;
  cancellationRequested: boolean;
  startedAt: string;
  finishedAt?: string | null;
  errorMessage?: string | null;
  totalFiles: number;
  processedFiles: number;
  totalBytes: number;
  processedBytes: number;
  preparedChunkReferences: number;
  currentPath?: string | null;
}

export interface SnapshotDeletionResult {
  deleted: boolean;
  errorMessage?: string | null;
  backupId: string;
  deletedSnapshotFiles: number;
  deletedSnapshotFileBytes: number;
}

export interface DownloadTicket {
  ticket: string;
  expiresAt: string;
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

export enum BackupProgressStage {
  Listing = 0,
  Preparing = 1,
  Reading = 2,
  Hashing = 3,
  Compressing = 4,
  Encrypting = 5,
  Uploading = 6,
  Recording = 7,
  Persisting = 8,
  Finalizing = 9,
  Completed = 10,
  Failed = 11,
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

export interface BackupScheduleItem {
  id: string;
  backupId: string;
  startAt: string;
  interval?: string | null;
  status: BackupStatus;
  finishedAt?: string | null;
  errorMessage?: string | null;
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
  lastProgressAt: string;
  noProgressFor: string;
  status: BackupStatus;
  stage: BackupProgressStage;
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

export interface SnapshotFilePage {
  items: SnapshotFileDto[];
  nextCursor?: string | null;
  hasNextPage: boolean;
  totalCount: number;
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
  missingIndexedObjects: number;
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
