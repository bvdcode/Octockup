import type { TokenPair } from "@bvdcode/react-kit";

export const COOKIE_SESSION_TOKEN = "cookie-session";

export function toCookieSession(tokens: TokenPair): TokenPair {
  return {
    accessToken: tokens.accessToken,
    refreshToken: COOKIE_SESSION_TOKEN,
  };
}
