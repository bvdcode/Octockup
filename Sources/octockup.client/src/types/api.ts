// Unified module destination enum matching backend ModuleDestination
export enum ModuleDestination {
  Source = 1,
  Target = 2,
}

// Module returned by /api/v1/modules
export interface Module {
  id: string;
  createdAt: string; // assuming BaseEntity exposes createdAt
  userId: string;
  tag: string;
  backupModuleId: string;
  type: ModuleDestination; // backend property 'Type'
}

// Provider metadata returned by /api/v1/modules/providers
export interface ModuleProviderInfo {
  id: string; // .NET FullName
  pathSeparator: string; // char mapped to string
  name: string;
  requiredParameters: string[];
}

// Test result item (kept for future usage)
export interface TestResultItem {
  path: string;
  name: string;
  size: number;
  lastModified?: string; // ISO string
}

// Request body for creating module
export interface CreateModuleRequest {
  destination: ModuleDestination;
  tag: string;
  backupModuleId: string;
  parameters: Record<string, string>;
}
