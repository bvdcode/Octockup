import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import AuthenticationSettingsCard from "./AuthenticationSettingsCard";

const mocks = vi.hoisted(() => ({
  confirm: vi.fn(),
  getAuthenticationSettings: vi.fn(),
  updateAuthenticationSettings: vi.fn(),
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock("material-ui-confirm", () => ({
  useConfirm: () => mocks.confirm,
}));

vi.mock("../../api/authApi", () => ({
  useAuthApi: () => ({
    getAuthenticationSettings: mocks.getAuthenticationSettings,
    updateAuthenticationSettings: mocks.updateAuthenticationSettings,
  }),
}));

describe("AuthenticationSettingsCard", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.confirm.mockReset();
    mocks.getAuthenticationSettings.mockReset();
    mocks.updateAuthenticationSettings.mockReset();
    mocks.getAuthenticationSettings.mockResolvedValue({
      passwordLoginEnabled: true,
    });
    mocks.updateAuthenticationSettings.mockResolvedValue({
      passwordLoginEnabled: false,
    });
  });

  it("updates password login only after disabling is confirmed", async () => {
    const user = userEvent.setup();
    let resolveConfirmation:
      | ((result: { confirmed: boolean }) => void)
      | undefined;
    mocks.confirm.mockReturnValue(
      new Promise((resolve) => {
        resolveConfirmation = resolve;
      }),
    );
    render(<AuthenticationSettingsCard />);

    const passwordLogin = await screen.findByRole("switch", {
      name: "settings.authentication.passwordLogin",
    });
    await user.click(passwordLogin);

    expect(mocks.confirm).toHaveBeenCalledOnce();
    expect(mocks.updateAuthenticationSettings).not.toHaveBeenCalled();

    resolveConfirmation?.({ confirmed: true });

    await waitFor(() => {
      expect(mocks.updateAuthenticationSettings).toHaveBeenCalledOnce();
    });
    expect(mocks.updateAuthenticationSettings).toHaveBeenCalledWith({
      passwordLoginEnabled: false,
    });
  });

  it("keeps password login enabled when disabling is cancelled", async () => {
    const user = userEvent.setup();
    mocks.confirm.mockResolvedValue({ confirmed: false });
    render(<AuthenticationSettingsCard />);

    const passwordLogin = await screen.findByRole("switch", {
      name: "settings.authentication.passwordLogin",
    });
    await user.click(passwordLogin);

    await waitFor(() => {
      expect(mocks.confirm).toHaveBeenCalledOnce();
    });
    expect(mocks.updateAuthenticationSettings).not.toHaveBeenCalled();
    expect(passwordLogin).toBeChecked();
  });
});
