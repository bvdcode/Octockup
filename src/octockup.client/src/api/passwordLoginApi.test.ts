import { beforeEach, describe, expect, it, vi } from "vitest";
import { loginWithPassword } from "./passwordLoginApi";

const axiosMocks = vi.hoisted(() => ({
  post: vi.fn(),
}));

vi.mock("axios", () => ({
  default: {
    create: () => ({ post: axiosMocks.post }),
  },
}));

describe("loginWithPassword", () => {
  beforeEach(() => {
    axiosMocks.post.mockReset();
  });

  it("uses the public login endpoint without authenticated interceptors", async () => {
    axiosMocks.post.mockResolvedValue({
      data: { accessToken: "access-token", refreshToken: "server-cookie" },
    });

    const result = await loginWithPassword({
      username: "user",
      password: "password",
    });

    expect(axiosMocks.post).toHaveBeenCalledWith("/api/v1/auth/login", {
      username: "user",
      password: "password",
    });
    expect(result).toEqual({
      accessToken: "access-token",
      refreshToken: "cookie-session",
    });
  });

  it("preserves the invalid-credentials response", async () => {
    const invalidCredentials = new Error("Invalid username or password.");
    axiosMocks.post.mockRejectedValue(invalidCredentials);

    await expect(
      loginWithPassword({ username: "user", password: "wrong-password" }),
    ).rejects.toBe(invalidCredentials);
  });
});
