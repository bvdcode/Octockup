import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import PasswordLoginForm from "./PasswordLoginForm";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

describe("PasswordLoginForm", () => {
  it("trims the username and submits the password", async () => {
    const user = userEvent.setup();
    const onSubmit = vi
      .fn<(username: string, password: string) => Promise<void>>()
      .mockResolvedValue();
    render(<PasswordLoginForm loading={false} onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/auth.username/), "  vadim  ");
    await user.type(screen.getByLabelText(/auth.password/), "secret");
    await user.click(screen.getByRole("button", { name: "auth.signIn" }));

    expect(onSubmit).toHaveBeenCalledWith("vadim", "secret");
  });
});
