import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import AdminSettingsSection from "./AdminSettingsSection";

vi.mock("./AuthenticationSettingsCard", () => ({
  default: () => <div>authentication-settings</div>,
}));

vi.mock("./OidcProvidersCard", () => ({
  default: () => <div>oidc-providers</div>,
}));

vi.mock("./UserManagementCard", () => ({
  default: () => <div>user-management</div>,
}));

vi.mock("./StorageCleanupCard", () => ({
  default: () => <div>storage-cleanup</div>,
}));

describe("AdminSettingsSection", () => {
  it("renders no administration controls for a non-admin user", () => {
    const result = render(
      <AdminSettingsSection isAdmin={false} onProvidersChanged={vi.fn()} />,
    );

    expect(result.container).toBeEmptyDOMElement();
  });

  it("renders storage cleanup controls for an administrator", () => {
    render(<AdminSettingsSection isAdmin onProvidersChanged={vi.fn()} />);

    expect(screen.getByText("storage-cleanup")).toBeInTheDocument();
  });
});
