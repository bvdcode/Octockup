import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import OidcProvidersCard from "./OidcProvidersCard";
import { renderWithQueryClient } from "../../test/renderWithQueryClient";

const authApiMocks = vi.hoisted(() => ({
  listOidcProviders: vi.fn(),
  deleteOidcProvider: vi.fn(),
}));

vi.mock("../../api/authApi", () => ({
  useAuthApi: () => authApiMocks,
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string, options?: { provider?: string }) =>
      options?.provider ? `${key}:${options.provider}` : key,
  }),
}));

describe("OidcProvidersCard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authApiMocks.listOidcProviders.mockResolvedValue([
      {
        id: "provider-id",
        name: "Company",
        slug: "company",
        issuer: "https://identity.example.com",
        publicBaseUrl: "https://octockup.example.com",
        callbackUrl: "https://octockup.example.com/api/v1/auth/oidc/callback",
        clientId: "octockup",
        scopes: ["openid"],
        isEnabled: true,
        hasClientSecret: true,
      },
    ]);
  });

  it("shows a provider deletion failure inside the confirmation dialog", async () => {
    authApiMocks.deleteOidcProvider.mockRejectedValue(
      new Error("Provider has linked accounts"),
    );
    const user = userEvent.setup();

    renderWithQueryClient(
      <OidcProvidersCard onProvidersChanged={vi.fn()} />,
    );

    await screen.findByText("Company");
    const deleteButton = screen.getByTestId("DeleteIcon").closest("button");
    if (deleteButton === null) {
      throw new Error("Delete button was not found");
    }
    await user.click(deleteButton);
    const dialog = screen.getByRole("dialog");

    await user.click(
      within(dialog).getByRole("button", { name: "common.delete" }),
    );

    expect(
      await within(dialog).findByText("Provider has linked accounts"),
    ).toBeInTheDocument();
  });
});
