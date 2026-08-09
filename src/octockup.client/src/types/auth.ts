import type { TokenPair } from "@bvdcode/react-kit";

export interface PublicOidcProvider {
  slug: string;
  name: string;
}

export interface AuthenticationOptions {
  passwordLoginEnabled: boolean;
  oidcProviders: PublicOidcProvider[];
}

export interface CurrentUser {
  id: string;
  username: string;
  displayName: string;
  avatarUrl?: string;
  isAdmin: boolean;
  isDisabled: boolean;
}

export interface OidcAuthorizationRequest {
  returnUrl: string;
  linkAccount: boolean;
}

export interface OidcAuthorizationResponse {
  authorizationUrl: string;
}

export interface ExternalIdentity {
  id: string;
  providerId: string;
  providerSlug: string;
  providerName: string;
  email?: string | null;
  displayName?: string | null;
  createdAt: string;
}

export interface AuthenticationSettings {
  passwordLoginEnabled: boolean;
}

export interface OidcProvider {
  id: string;
  name: string;
  slug: string;
  issuer: string;
  publicBaseUrl: string;
  callbackUrl: string;
  clientId: string;
  scopes: string[];
  isEnabled: boolean;
  hasClientSecret: boolean;
}

export interface SaveOidcProviderRequest {
  name: string;
  slug?: string;
  issuer: string;
  publicBaseUrl: string;
  clientId: string;
  clientSecret?: string;
  clearClientSecret: boolean;
  scopes: string[];
  isEnabled: boolean;
}

export interface OidcProviderFormValues {
  name: string;
  slug: string;
  issuer: string;
  publicBaseUrl: string;
  clientId: string;
  clientSecret: string;
  clearClientSecret: boolean;
  scopes: string;
  isEnabled: boolean;
}

export interface AdminUser {
  id: string;
  username: string;
  isAdmin: boolean;
  isDisabled: boolean;
  externalIdentityCount: number;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  isAdmin: boolean;
}

export interface UpdateUserAccessRequest {
  isAdmin: boolean;
  isDisabled: boolean;
}

export interface ChangePasswordRequest {
  oldPassword: string;
  newPassword: string;
}

export interface ApiErrorResponse {
  message?: string;
  detail?: string;
}

export type OidcCallbackStatus = "success" | "linked" | "error" | null;

export type RefreshSessionResponse = TokenPair;
