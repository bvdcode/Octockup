import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import LoginMethods from "./LoginMethods";

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string, options?: { provider?: string }) =>
      options?.provider ? `${key}:${options.provider}` : key,
  }),
}));

describe("LoginMethods", () => {
  it("hides password fields and offers OIDC when password login is disabled", () => {
    const onPasswordLogin = vi.fn(() => Promise.resolve());
    const onOidcLogin = vi.fn(() => Promise.resolve());

    render(
      <LoginMethods
        options={{
          passwordLoginEnabled: false,
          oidcProviders: [{ slug: "company", name: "Company" }],
        }}
        loadingPassword={false}
        loadingSlug={null}
        onPasswordLogin={onPasswordLogin}
        onOidcLogin={onOidcLogin}
      />,
    );

    expect(screen.queryByLabelText(/auth.username/)).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "auth.continueWith:Company" }),
    ).toBeInTheDocument();
  });
});
