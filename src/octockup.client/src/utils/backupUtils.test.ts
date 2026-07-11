import { describe, expect, it } from "vitest";
import type { BackupItem, Module, ScheduleReport } from "../types/api";
import { BackupStatus, ModuleDestination } from "../types/api";
import { getBackupOverallStatus } from "./backupUtils";

const source = createModule("source", ModuleDestination.Source);
const storage = createModule("storage", ModuleDestination.Target);
const emptyReports = new Map<string, ScheduleReport>();

describe("getBackupOverallStatus", () => {
  it("uses the active schedule summary", () => {
    const running = createBackup({
      activeSchedule: {
        id: "running",
        backupId: "backup",
        startAt: "2026-01-01T00:00:00Z",
        status: BackupStatus.Running,
      },
    });
    const scheduled = createBackup({
      activeSchedule: {
        id: "created",
        backupId: "backup",
        startAt: "2026-01-01T00:00:00Z",
        status: BackupStatus.Created,
      },
    });

    expect(getStatus(running)).toBe("running");
    expect(getStatus(scheduled)).toBe("scheduled");
  });

  it("reports the latest failure when no completed snapshot exists", () => {
    const backup = createBackup({
      latestFinishedSchedule: {
        id: "failed",
        backupId: "backup",
        startAt: "2026-01-01T00:00:00Z",
        finishedAt: "2026-01-01T00:01:00Z",
        status: BackupStatus.Failed,
        errorMessage: "failed",
      },
    });

    expect(getStatus(backup)).toBe("failed");
  });

  it("distinguishes failures before and after the latest snapshot", () => {
    const latestSnapshot = {
      id: "snapshot",
      backupId: "backup",
      completedAt: "2026-01-02T00:00:00Z",
      filesCount: 10,
      totalSize: 100,
    };
    const failure = {
      id: "failed",
      backupId: "backup",
      startAt: "2026-01-01T00:00:00Z",
      finishedAt: "2026-01-03T00:00:00Z",
      status: BackupStatus.Failed,
      errorMessage: "failed",
    };
    const warning = createBackup({ latestSnapshot, latestFinishedSchedule: failure });
    const success = createBackup({
      latestSnapshot,
      latestFinishedSchedule: {
        ...failure,
        finishedAt: "2026-01-01T00:00:00Z",
      },
    });

    expect(getStatus(warning)).toBe("warning");
    expect(getStatus(success)).toBe("success");
  });
});

function getStatus(backup: BackupItem) {
  return getBackupOverallStatus(backup, {}, emptyReports);
}

function createBackup(overrides: Partial<BackupItem>): BackupItem {
  return {
    id: "backup",
    tag: "backup",
    sourceId: source.id,
    storageId: storage.id,
    ignoredPaths: [],
    disableCompression: false,
    disableEncryption: false,
    source,
    storage,
    snapshotCount: 0,
    completedSnapshotCount: 0,
    scheduleCount: 0,
    ...overrides,
  };
}

function createModule(id: string, destination: ModuleDestination): Module {
  return {
    id,
    createdAt: "2026-01-01T00:00:00Z",
    userId: "user",
    tag: id,
    backupModuleId: id,
    destination,
  };
}
