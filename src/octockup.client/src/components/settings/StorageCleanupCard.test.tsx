import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { StorageCleanupStatus } from "../../types/storageCleanup";
import StorageCleanupCard from "./StorageCleanupCard";

const api = vi.hoisted(() => ({
  list: vi.fn(),
  start: vi.fn(),
}));
const translation = vi.hoisted(() => ({
  t: (key: string) => key,
}));

vi.mock("../../api/storageCleanupApi", () => ({
  useStorageCleanupApi: () => api,
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => translation,
}));

describe("StorageCleanupCard", () => {
  beforeEach(() => {
    api.list.mockReset();
    api.start.mockReset();
  });

  it("shows persistent cleanup statistics and starts a storage cleanup", async () => {
    const idleCleanup = {
      id: "cleanup-id",
      moduleId: "storage-id",
      moduleTag: "Archive storage",
      status: StorageCleanupStatus.Completed,
      scannedChunks: 12000,
      pendingChunks: 4,
      totalDeletedChunks: 80,
      totalReclaimedBytes: 4096,
      createdAt: "2026-08-08T00:00:00Z",
      updatedAt: "2026-08-08T01:00:00Z",
    };
    api.list.mockResolvedValue([idleCleanup]);
    api.start.mockResolvedValue({
      ...idleCleanup,
      status: StorageCleanupStatus.Running,
      scannedChunks: 0,
    });

    render(<StorageCleanupCard />);

    expect(await screen.findByText("Archive storage")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "settings.cleanup.start" }));

    await waitFor(() => {
      expect(api.start).toHaveBeenCalledWith("storage-id");
    });
    expect(await screen.findByText("settings.cleanup.running")).toBeDisabled();
  });
});
