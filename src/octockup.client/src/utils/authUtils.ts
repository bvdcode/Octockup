import type {
  ExternalIdentity,
  OidcCallbackStatus,
  PublicOidcProvider,
  OidcProviderFormValues,
  SaveOidcProviderRequest,
} from "../types/auth";

export function getOidcCallbackStatus(search: string): OidcCallbackStatus {
  const value = new URLSearchParams(search).get("oidc");

  switch (value) {
    case "success":
    case "linked":
    case "error":
      return value;
    default:
      return null;
  }
}

export function clearOidcCallbackStatus(): void {
  const url = new URL(window.location.href);
  url.searchParams.delete("oidc");
  const pathname = url.pathname === "/login" ? "/" : url.pathname;
  window.history.replaceState(null, "", `${pathname}${url.search}${url.hash}`);
}

export function getCurrentReturnUrl(): string {
  if (window.location.pathname === "/login") {
    return "/";
  }

  const url = new URL(window.location.href);
  url.searchParams.delete("oidc");
  const relativeUrl = `${url.pathname}${url.search}${url.hash}`;
  return relativeUrl.startsWith("/") ? relativeUrl : "/";
}

export function getUnlinkedProviders(
  providers: PublicOidcProvider[],
  identities: ExternalIdentity[],
): PublicOidcProvider[] {
  const linkedProviderIds = new Set(
    identities.map((identity) => identity.providerSlug),
  );
  return providers.filter((provider) => !linkedProviderIds.has(provider.slug));
}

export function parseScopes(value: string): string[] {
  return [...new Set(value.trim().split(/\s+/).filter(Boolean))];
}

export function buildOidcCallbackUrl(publicBaseUrl: string): string {
  return `${publicBaseUrl.trim().replace(/\/+$/, "")}/api/v1/auth/oidc/callback`;
}

export function toOidcProviderRequest(
  values: OidcProviderFormValues,
): SaveOidcProviderRequest {
  const clientSecret = values.clientSecret.trim();
  return {
    name: values.name.trim(),
    slug: values.slug.trim() || undefined,
    issuer: values.issuer.trim(),
    publicBaseUrl: values.publicBaseUrl.trim(),
    clientId: values.clientId.trim(),
    clientSecret: values.clearClientSecret
      ? undefined
      : clientSecret || undefined,
    clearClientSecret: values.clearClientSecret,
    scopes: parseScopes(values.scopes),
    isEnabled: values.isEnabled,
  };
}
