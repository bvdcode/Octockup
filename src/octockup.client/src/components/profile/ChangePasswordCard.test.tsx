import { cleanup, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import ChangePasswordCard from "./ChangePasswordCard";
import { renderWithQueryClient } from "../../test/renderWithQueryClient";

const authApi = vi.hoisted(() => ({
  changePassword: vi.fn(),
}));

vi.mock("../../api/authApi", () => ({
  useAuthApi: () => authApi,
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

describe("ChangePasswordCard", () => {
  afterEach(cleanup);

  beforeEach(() => {
    authApi.changePassword.mockReset();
    authApi.changePassword.mockResolvedValue(undefined);
  });

  it("rejects mismatched new passwords without sending a request", async () => {
    const user = userEvent.setup();
    renderWithQueryClient(<ChangePasswordCard />);

    await user.type(
      screen.getByLabelText("profile.password.current"),
      "current-password",
    );
    await user.type(
      screen.getByLabelText("profile.password.new"),
      "new-password",
    );
    await user.type(
      screen.getByLabelText("profile.password.confirm"),
      "different-password",
    );
    await user.click(
      screen.getByRole("button", { name: "profile.password.save" }),
    );

    expect(
      screen.getByText("profile.password.mismatch"),
    ).toBeInTheDocument();
    expect(authApi.changePassword).not.toHaveBeenCalled();
  });

  it("trims credentials and clears the form after a successful change", async () => {
    const user = userEvent.setup();
    renderWithQueryClient(<ChangePasswordCard />);

    const currentPassword = screen.getByLabelText("profile.password.current");
    const newPassword = screen.getByLabelText("profile.password.new");
    const confirmation = screen.getByLabelText("profile.password.confirm");
    await user.type(currentPassword, " current-password ");
    await user.type(newPassword, " new-password ");
    await user.type(confirmation, " new-password ");
    await user.click(
      screen.getByRole("button", { name: "profile.password.save" }),
    );

    await waitFor(() => {
      expect(authApi.changePassword).toHaveBeenCalledWith({
        oldPassword: "current-password",
        newPassword: "new-password",
      });
    });
    expect(screen.getByText("profile.password.saved")).toBeInTheDocument();
    expect(currentPassword).toHaveValue("");
    expect(newPassword).toHaveValue("");
    expect(confirmation).toHaveValue("");
  });
});
