import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import StorageCleanupCard from "./StorageCleanupCard";

const navigate = vi.hoisted(() => vi.fn());

vi.mock("react-router-dom", () => ({
  useNavigate: () => navigate,
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe("StorageCleanupCard", () => {
  it("opens the technical cleanup dashboard", () => {
    render(<StorageCleanupCard />);

    fireEvent.click(
      screen.getByRole("button", { name: "settings.cleanup.openDashboard" }),
    );

    expect(navigate).toHaveBeenCalledWith("/admin/storage-cleanup");
  });
});
