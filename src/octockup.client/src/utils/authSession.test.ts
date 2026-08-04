import { describe, expect, it } from "vitest";
import { COOKIE_SESSION_TOKEN, toCookieSession } from "./authSession";

describe("cookie-backed authentication session", () => {
  it("never returns a raw refresh token to the client store", () => {
    const session = toCookieSession({
      accessToken: "access-token",
      refreshToken: "raw-refresh-token",
    });

    expect(session).toEqual({
      accessToken: "access-token",
      refreshToken: COOKIE_SESSION_TOKEN,
    });
    expect(session.refreshToken).not.toBe("raw-refresh-token");
  });
});
