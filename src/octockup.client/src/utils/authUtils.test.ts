import { describe, expect, it } from "vitest";
import type { ExternalIdentity, PublicOidcProvider } from "../types/auth";
import {
  buildOidcCallbackUrl,
  clearOidcCallbackStatus,
  getCurrentReturnUrl,
  getOidcCallbackStatus,
  getUnlinkedProviders,
  parseScopes,
  toOidcProviderRequest,
} from "./authUtils";

describe("auth utilities", () => {
  it("accepts only known OIDC callback markers", () => {
    expect(getOidcCallbackStatus("?oidc=success")).toBe("success");
    expect(getOidcCallbackStatus("?oidc=linked")).toBe("linked");
    expect(getOidcCallbackStatus("?oidc=error")).toBe("error");
    expect(getOidcCallbackStatus("?oidc=unexpected")).toBeNull();
  });

  it("never returns the unregistered login route after an OIDC failure", () => {
    window.history.replaceState(null, "", "/login?oidc=error&from=settings");

    expect(getCurrentReturnUrl()).toBe("/");
    clearOidcCallbackStatus();
    expect(`${window.location.pathname}${window.location.search}`).toBe(
      "/?from=settings",
    );
  });

  it("removes stale OIDC markers from the next return URL", () => {
    window.history.replaceState(
      null,
      "",
      "/settings?view=authentication&oidc=linked&oidc=success#providers",
    );

    expect(getCurrentReturnUrl()).toBe(
      "/settings?view=authentication#providers",
    );
  });

  it("offers only providers that are not already linked", () => {
    const providers: PublicOidcProvider[] = [
      { slug: "company", name: "Company" },
      { slug: "personal", name: "Personal" },
    ];
    const identities: ExternalIdentity[] = [
      {
        id: "identity-id",
        providerId: "provider-id",
        providerSlug: "company",
        providerName: "Company",
        createdAt: "2026-08-04T00:00:00Z",
      },
    ];

    expect(getUnlinkedProviders(providers, identities)).toEqual([
      { slug: "personal", name: "Personal" },
    ]);
  });

  it("normalizes scopes and callback URL", () => {
    expect(parseScopes(" openid   profile openid email ")).toEqual([
      "openid",
      "profile",
      "email",
    ]);
    expect(buildOidcCallbackUrl("https://backup.example.com/")).toBe(
      "https://backup.example.com/api/v1/auth/oidc/callback",
    );
  });

  it("preserves an existing provider secret when edit leaves it blank", () => {
    const request = toOidcProviderRequest({
      name: "Company",
      slug: "company",
      issuer: "https://id.example.com",
      publicBaseUrl: "https://backup.example.com",
      clientId: "octockup",
      clientSecret: "",
      clearClientSecret: false,
      scopes: "openid profile",
      isEnabled: true,
    });

    expect(request.clientSecret).toBeUndefined();
    expect(request.clearClientSecret).toBe(false);
  });

  it("never sends a replacement secret together with the clear flag", () => {
    const request = toOidcProviderRequest({
      name: "Company",
      slug: "company",
      issuer: "https://id.example.com",
      publicBaseUrl: "https://backup.example.com",
      clientId: "octockup",
      clientSecret: "replacement-secret",
      clearClientSecret: true,
      scopes: "openid",
      isEnabled: true,
    });

    expect(request.clientSecret).toBeUndefined();
    expect(request.clearClientSecret).toBe(true);
  });
});
