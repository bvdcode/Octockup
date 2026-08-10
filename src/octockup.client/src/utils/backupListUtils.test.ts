import { describe, expect, it } from "vitest";
import {
  ModuleDestination,
  type BackupItem,
  type SnapshotDto,
} from "../types/api";
import { BackupSortOption } from "../types/backupList";
import type { BackupOverallStatus } from "./backupUtils";
import {
  filterBackups,
  getLatestCompletedSnapshot,
  sortBackups,
} from "./backupListUtils";

function createBackup(
  id: string,
  snapshots: SnapshotDto[] = [],
  storageId = "storage-one",
): BackupItem {
  return {
    id,
    tag: id,
    sourceId: `source-${id}`,
    storageId,
    ignoredPaths: [],
    disableCompression: false,
    disableEncryption: false,
    source: {
      id: `source-${id}`,
      createdAt: "2026-08-01T00:00:00Z",
      userId: "user-id",
      tag: `Source ${id}`,
      backupModuleId: "source-provider",
      destination: ModuleDestination.Source,
    },
    storage: {
      id: storageId,
      createdAt: "2026-08-01T00:00:00Z",
      userId: "user-id",
      tag: storageId === "storage-one" ? "Primary storage" : "Archive storage",
      backupModuleId: "storage-provider",
      destination: ModuleDestination.Target,
    },
    schedules: [],
    snapshots,
  };
}

function createSnapshot(
  backupId: string,
  id: string,
  completedAt: string | null,
  totalSize: number,
): SnapshotDto {
  return {
    id,
    backupId,
    completedAt,
    filesCount: 1,
    totalSize,
  };
}

const successStatus = (): BackupOverallStatus => "success";

describe("backupListUtils", () => {
  it("uses the latest completed snapshot for recency and logical size", () => {
    const backup = createBackup("backup", [
      createSnapshot("backup", "new-incomplete", null, 9_000),
      createSnapshot("backup", "new-complete", "2026-08-03T00:00:00Z", 300),
      createSnapshot("backup", "old-complete", "2026-08-01T00:00:00Z", 100),
    ]);

    expect(getLatestCompletedSnapshot(backup)?.id).toBe("new-complete");
  });

  it("sorts recent backups newest first and never-run backups last", () => {
    const newest = createBackup("newest", [
      createSnapshot("newest", "newest-snapshot", "2026-08-03T00:00:00Z", 10),
    ]);
    const older = createBackup("older", [
      createSnapshot("older", "older-snapshot", "2026-08-01T00:00:00Z", 20),
    ]);
    const never = createBackup("never");

    const sorted = sortBackups(
      [older, never, newest],
      BackupSortOption.Recent,
      successStatus,
    );

    expect(sorted.map((backup) => backup.id)).toEqual(["newest", "older", "never"]);
  });

  it("puts never-run backups first when sorting by oldest", () => {
    const newer = createBackup("newer", [
      createSnapshot("newer", "newer-snapshot", "2026-08-03T00:00:00Z", 10),
    ]);
    const older = createBackup("older", [
      createSnapshot("older", "older-snapshot", "2026-08-01T00:00:00Z", 20),
    ]);
    const never = createBackup("never");

    const sorted = sortBackups(
      [newer, older, never],
      BackupSortOption.Oldest,
      successStatus,
    );

    expect(sorted.map((backup) => backup.id)).toEqual(["never", "older", "newer"]);
  });

  it("sorts by the latest snapshot logical size", () => {
    const large = createBackup("large", [
      createSnapshot("large", "large-old", "2026-08-01T00:00:00Z", 1),
      createSnapshot("large", "large-latest", "2026-08-02T00:00:00Z", 500),
    ]);
    const small = createBackup("small", [
      createSnapshot("small", "small-latest", "2026-08-02T00:00:00Z", 50),
    ]);

    expect(
      sortBackups([small, large], BackupSortOption.Largest, successStatus).map(
        (backup) => backup.id,
      ),
    ).toEqual(["large", "small"]);
    expect(
      sortBackups([large, small], BackupSortOption.Smallest, successStatus).map(
        (backup) => backup.id,
      ),
    ).toEqual(["small", "large"]);
  });

  it("prioritizes active and unhealthy backups in smart order", () => {
    const running = createBackup("running");
    const failed = createBackup("failed");
    const successful = createBackup("successful");
    const statuses: Record<string, BackupOverallStatus> = {
      running: "running",
      failed: "failed",
      successful: "success",
    };

    const sorted = sortBackups(
      [successful, failed, running],
      BackupSortOption.Smart,
      (backup) => statuses[backup.id],
    );

    expect(sorted.map((backup) => backup.id)).toEqual([
      "running",
      "failed",
      "successful",
    ]);
  });

  it("filters by storage and searches backup, source, and storage names", () => {
    const primary = createBackup("documents");
    const archive = createBackup("photos", [], "storage-two");

    expect(filterBackups([primary, archive], "storage-two", "")).toEqual([
      archive,
    ]);
    expect(filterBackups([primary, archive], null, "source photos")).toEqual([
      archive,
    ]);
    expect(filterBackups([primary, archive], null, "PRIMARY")).toEqual([
      primary,
    ]);
  });
});
