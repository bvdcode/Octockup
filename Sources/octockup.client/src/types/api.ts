export interface BackupSource {
  id: string;
  name: string;
  parameters: string[];
  pathSeparator: string;
}

export interface BackupStorage {
  id: string;
  name: string;
  parameters: string[];
  pathSeparator: string;
}

export interface TestResultItem {
  path: string;
  name: string;
  size: number;
  lastModified?: string; // ISO string
}
