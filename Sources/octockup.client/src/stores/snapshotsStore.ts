import { create } from "zustand";
import type { SnapshotDto } from "../api/snapshotsApi";

interface SnapshotsState {
  snapshots: Record<string, SnapshotDto[]>;
  setSnapshots: (backupId: string, data: SnapshotDto[]) => void;
  clearSnapshots: (backupId: string) => void;
  getSnapshots: (backupId: string) => SnapshotDto[] | undefined;
}

export const useSnapshotsStore = create<SnapshotsState>((set, get) => ({
  snapshots: {},
  setSnapshots: (backupId, data) =>
    set((state) => ({
      snapshots: { ...state.snapshots, [backupId]: data },
    })),
  clearSnapshots: (backupId) =>
    set((state) => {
      const { [backupId]: _, ...rest } = state.snapshots;
      return { snapshots: rest };
    }),
  getSnapshots: (backupId) => get().snapshots[backupId],
}));
