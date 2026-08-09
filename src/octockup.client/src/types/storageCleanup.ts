export enum StorageCleanupStatus {
  Idle = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
}

export interface StorageCleanup {
  id: string;
  moduleId: string;
  moduleTag: string;
  status: StorageCleanupStatus;
  scannedChunks: number;
  pendingChunks: number;
  totalDeletedChunks: number;
  totalReclaimedBytes: number;
  lastStartedAt?: string | null;
  lastCompletedAt?: string | null;
  errorMessage?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface StorageCleanupRun {
  id: string;
  moduleId: string;
  moduleTag: string;
  status: StorageCleanupStatus;
  startedAt: string;
  completedAt?: string | null;
  scannedChunks: number;
  deletedChunks: number;
  reclaimedBytes: number;
  errorMessage?: string | null;
  createdAt: string;
  updatedAt: string;
}
