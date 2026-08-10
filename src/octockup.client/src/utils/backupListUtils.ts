import type { BackupItem, SnapshotDto } from "../types/api";
import { BackupSortOption } from "../types/backupList";
import type { BackupOverallStatus } from "./backupUtils";

export function parseBackupSortOption(value: string | null): BackupSortOption {
  switch (value) {
    case BackupSortOption.Recent:
      return BackupSortOption.Recent;
    case BackupSortOption.Oldest:
      return BackupSortOption.Oldest;
    case BackupSortOption.Largest:
      return BackupSortOption.Largest;
    case BackupSortOption.Smallest:
      return BackupSortOption.Smallest;
    case BackupSortOption.Name:
      return BackupSortOption.Name;
    case BackupSortOption.Smart:
    case null:
      return BackupSortOption.Smart;
    default:
      return BackupSortOption.Smart;
  }
}

export function getLatestCompletedSnapshot(
  backup: BackupItem,
): SnapshotDto | null {
  return backup.snapshots.reduce<SnapshotDto | null>((latest, snapshot) => {
    if (!snapshot.completedAt) {
      return latest;
    }
    if (!latest?.completedAt) {
      return snapshot;
    }
    return new Date(snapshot.completedAt).getTime() >
      new Date(latest.completedAt).getTime()
      ? snapshot
      : latest;
  }, null);
}

export function filterBackups(
  backups: BackupItem[],
  storageId: string | null,
  search: string,
): BackupItem[] {
  const normalizedSearch = search.trim().toLocaleLowerCase();
  return backups.filter((backup) => {
    if (storageId && backup.storageId !== storageId) {
      return false;
    }
    if (!normalizedSearch) {
      return true;
    }
    return [backup.tag, backup.source.tag, backup.storage.tag].some((value) =>
      value.toLocaleLowerCase().includes(normalizedSearch),
    );
  });
}

export function sortBackups(
  backups: BackupItem[],
  sort: BackupSortOption,
  getStatus: (backup: BackupItem) => BackupOverallStatus,
): BackupItem[] {
  return backups.slice().sort((left, right) => {
    const result = compareBackups(left, right, sort, getStatus);
    return result !== 0 ? result : left.tag.localeCompare(right.tag);
  });
}

function compareBackups(
  left: BackupItem,
  right: BackupItem,
  sort: BackupSortOption,
  getStatus: (backup: BackupItem) => BackupOverallStatus,
): number {
  const leftSnapshot = getLatestCompletedSnapshot(left);
  const rightSnapshot = getLatestCompletedSnapshot(right);
  const leftCompletedAt = toTimestamp(leftSnapshot?.completedAt);
  const rightCompletedAt = toTimestamp(rightSnapshot?.completedAt);

  switch (sort) {
    case BackupSortOption.Smart: {
      const statusDifference =
        getStatusPriority(getStatus(left)) - getStatusPriority(getStatus(right));
      return statusDifference !== 0
        ? statusDifference
        : compareTimestamps(leftCompletedAt, rightCompletedAt, false);
    }
    case BackupSortOption.Recent:
      return compareTimestamps(leftCompletedAt, rightCompletedAt, false);
    case BackupSortOption.Oldest:
      return compareTimestamps(leftCompletedAt, rightCompletedAt, true);
    case BackupSortOption.Largest:
      return (rightSnapshot?.totalSize ?? 0) - (leftSnapshot?.totalSize ?? 0);
    case BackupSortOption.Smallest:
      return (leftSnapshot?.totalSize ?? 0) - (rightSnapshot?.totalSize ?? 0);
    case BackupSortOption.Name:
      return left.tag.localeCompare(right.tag);
  }
}

function getStatusPriority(status: BackupOverallStatus): number {
  switch (status) {
    case "running":
      return 0;
    case "failed":
      return 1;
    case "warning":
      return 2;
    case "scheduled":
      return 3;
    case "success":
      return 4;
    case "created":
      return 5;
    case "idle":
      return 6;
  }
}

function toTimestamp(value: string | null | undefined): number | null {
  return value ? new Date(value).getTime() : null;
}

function compareTimestamps(
  left: number | null,
  right: number | null,
  nullsFirst: boolean,
): number {
  if (left === null && right === null) {
    return 0;
  }
  if (left === null) {
    return nullsFirst ? -1 : 1;
  }
  if (right === null) {
    return nullsFirst ? 1 : -1;
  }
  return nullsFirst ? left - right : right - left;
}
