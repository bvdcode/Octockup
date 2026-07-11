import { describe, expect, it } from "vitest";
import {
  SnapshotArchivePhase,
  SnapshotArchiveStatus,
  type SnapshotArchiveJob,
} from "../types/api";
import {
  getSnapshotArchiveProgressPercent,
  isSnapshotArchiveActive,
  isSnapshotArchiveTerminal,
} from "./snapshotArchiveUtils";

const baseJob: SnapshotArchiveJob = {
  jobId: "job",
  userId: "user",
  snapshotId: "snapshot",
  status: SnapshotArchiveStatus.Pending,
  phase: SnapshotArchivePhase.Waiting,
  cancellationRequested: false,
  startedAt: "2026-07-10T00:00:00Z",
  totalFiles: 100,
  processedFiles: 25,
  totalBytes: 1000,
  processedBytes: 400,
  preparedChunkReferences: 250,
};

describe("snapshotArchiveUtils", () => {
  it("identifies active and terminal archive jobs", () => {
    const running = {
      ...baseJob,
      status: SnapshotArchiveStatus.Running,
    };
    const completed = {
      ...baseJob,
      status: SnapshotArchiveStatus.Completed,
    };

    expect(isSnapshotArchiveActive(baseJob)).toBe(true);
    expect(isSnapshotArchiveActive(running)).toBe(true);
    expect(isSnapshotArchiveTerminal(running)).toBe(false);
    expect(isSnapshotArchiveTerminal(completed)).toBe(true);
  });

  it("uses byte progress while streaming and clamps overrun", () => {
    expect(getSnapshotArchiveProgressPercent(baseJob)).toBe(40);
    expect(
      getSnapshotArchiveProgressPercent({
        ...baseJob,
        processedBytes: 1200,
      }),
    ).toBe(100);
  });

  it("uses file progress while archive bytes are still zero", () => {
    expect(
      getSnapshotArchiveProgressPercent({
        ...baseJob,
        processedBytes: 0,
      }),
    ).toBe(25);
  });
});
