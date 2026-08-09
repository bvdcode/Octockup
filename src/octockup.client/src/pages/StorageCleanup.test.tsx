import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { StorageCleanupStatus } from "../types/storageCleanup";
import StorageCleanupPage from "./StorageCleanup";
import {
  createTestQueryClient,
  renderWithQueryClient,
} from "../test/renderWithQueryClient";

const api = vi.hoisted(() => ({
  list: vi.fn(),
  listRuns: vi.fn(),
  start: vi.fn(),
}));
const translation = vi.hoisted(() => ({
  t: (key: string) => key,
  i18n: { resolvedLanguage: "en" },
}));

vi.mock("../api/storageCleanupApi", () => ({
  useStorageCleanupApi: () => api,
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => translation,
}));

describe("StorageCleanupPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    api.list.mockReset();
    api.listRuns.mockReset();
    api.start.mockReset();
  });

  it("renders storage state and persistent run history", async () => {
    const cleanup = {
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
    const run = {
      id: "run-id",
      moduleId: "storage-id",
      moduleTag: "Archive storage",
      status: StorageCleanupStatus.Completed,
      startedAt: "2026-08-08T00:00:00Z",
      completedAt: "2026-08-08T00:10:00Z",
      scannedChunks: 12000,
      deletedChunks: 80,
      reclaimedBytes: 4096,
      createdAt: "2026-08-08T00:00:00Z",
      updatedAt: "2026-08-08T00:10:00Z",
    };
    api.list.mockResolvedValue([cleanup]);
    api.listRuns.mockResolvedValue([run]);
    api.start.mockResolvedValue({
      ...cleanup,
      status: StorageCleanupStatus.Running,
    });

    renderWithQueryClient(<StorageCleanupPage />);

    expect(await screen.findAllByText("Archive storage")).toHaveLength(2);
    expect(screen.getByText("storageCleanup.history.title")).toBeInTheDocument();
    fireEvent.click(
      screen.getByRole("button", { name: "storageCleanup.start" }),
    );

    await waitFor(() => {
      expect(api.start).toHaveBeenCalledWith("storage-id");
    });
  });

  it("reuses fresh cleanup data after the page is reopened", async () => {
    api.list.mockResolvedValue([]);
    api.listRuns.mockResolvedValue([]);
    const queryClient = createTestQueryClient();

    const firstView = renderWithQueryClient(
      <StorageCleanupPage />,
      queryClient,
    );
    await screen.findByText("storageCleanup.storages");
    firstView.unmount();

    renderWithQueryClient(<StorageCleanupPage />, queryClient);

    expect(screen.getByText("storageCleanup.storages")).toBeInTheDocument();
    expect(api.list).toHaveBeenCalledOnce();
    expect(api.listRuns).toHaveBeenCalledOnce();
  });
});
