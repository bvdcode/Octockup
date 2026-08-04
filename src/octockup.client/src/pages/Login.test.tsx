import { StrictMode } from "react";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import LoginPage from "./Login";

const apiMocks = vi.hoisted(() => ({
  getOptions: vi.fn(),
  refreshFromCookie: vi.fn(),
  beginOidcAuthorization: vi.fn(),
}));

const authStore = vi.hoisted(() => ({
  apiService: { getAxios: vi.fn() },
  login: vi.fn(),
  setAccessToken: vi.fn(),
  setRefreshToken: vi.fn(),
}));

vi.mock("@bvdcode/react-kit", () => ({
  useAuthStore: <T,>(selector: (state: typeof authStore) => T): T =>
    selector(authStore),
}));

vi.mock("../api/authApi", () => ({
  AuthApiClient: class {
    getOptions = apiMocks.getOptions;
    refreshFromCookie = apiMocks.refreshFromCookie;
    beginOidcAuthorization = apiMocks.beginOidcAuthorization;
  },
}));

vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (key: string, options?: { provider?: string }) =>
      options?.provider ? `${key}:${options.provider}` : key,
  }),
}));

describe("LoginPage", () => {
  afterEach(() => {
    cleanup();
  });

  beforeEach(() => {
    vi.clearAllMocks();
    window.history.replaceState(null, "", "/");
    apiMocks.getOptions.mockResolvedValue({
      passwordLoginEnabled: false,
      oidcProviders: [{ slug: "company", name: "Company" }],
    });
  });

  it("finishes loading when mounted under StrictMode", async () => {
    render(
      <StrictMode>
        <LoginPage />
      </StrictMode>,
    );

    expect(
      await screen.findByRole("button", {
        name: "auth.continueWith:Company",
      }),
    ).toBeInTheDocument();
    expect(apiMocks.getOptions).toHaveBeenCalledTimes(1);
  });

  it("allows retry after authentication options fail to load", async () => {
    apiMocks.getOptions
      .mockRejectedValueOnce(new Error("Authentication is unavailable"))
      .mockResolvedValueOnce({
        passwordLoginEnabled: false,
        oidcProviders: [{ slug: "company", name: "Company" }],
      });
    const user = userEvent.setup();

    render(<LoginPage />);

    expect(
      await screen.findByText("Authentication is unavailable"),
    ).toBeInTheDocument();
    expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "common.retry" }));

    expect(
      await screen.findByRole("button", {
        name: "auth.continueWith:Company",
      }),
    ).toBeInTheDocument();
    expect(apiMocks.getOptions).toHaveBeenCalledTimes(2);
  });
});
