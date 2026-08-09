import { fireEvent, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import OidcProviderDialog from "./OidcProviderDialog";
import { renderWithQueryClient } from "../../test/renderWithQueryClient";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe("OidcProviderDialog", () => {
  it("uses the browser origin as the public URL", async () => {
    const user = userEvent.setup();
    const onSave = vi.fn().mockResolvedValue(undefined);

    renderWithQueryClient(
      <OidcProviderDialog
        open
        provider={null}
        saving={false}
        error={null}
        onClose={vi.fn()}
        onSave={onSave}
      />,
    );

    fireEvent.change(screen.getByLabelText(/settings\.oidc\.name/), {
      target: { value: "Company" },
    });
    fireEvent.change(screen.getByLabelText(/settings\.oidc\.issuer/), {
      target: { value: "https://identity.example.com" },
    });
    fireEvent.change(screen.getByLabelText(/settings\.oidc\.clientId/), {
      target: { value: "client" },
    });
    await user.click(
      screen.getByRole("button", { name: "common.save" }),
    );

    expect(
      screen.getByLabelText("settings.oidc.callbackUrl"),
    ).toHaveValue(`${window.location.origin}/api/v1/auth/oidc/callback`);
    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ publicBaseUrl: window.location.origin }),
    );
  });
});
