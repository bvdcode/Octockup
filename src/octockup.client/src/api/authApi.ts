import type { AxiosInstance } from "axios";
import { useMemo } from "react";
import { useAxios } from "@bvdcode/react-kit";
import type {
  AdminUser,
  AuthenticationOptions,
  AuthenticationSettings,
  CreateUserRequest,
  CurrentUser,
  ExternalIdentity,
  OidcAuthorizationResponse,
  OidcProvider,
  RefreshSessionResponse,
  SaveOidcProviderRequest,
  UpdateUserAccessRequest,
} from "../types/auth";
import { toCookieSession } from "../utils/authSession";

export class AuthApiClient {
  constructor(private readonly axiosInstance: AxiosInstance) {}

  async getOptions(): Promise<AuthenticationOptions> {
    const response = await this.axiosInstance.get<AuthenticationOptions>(
      "/api/v1/auth/options",
    );
    return response.data;
  }

  async refreshFromCookie(): Promise<RefreshSessionResponse> {
    const response = await this.axiosInstance.post<RefreshSessionResponse>(
      "/api/v1/auth/refresh",
      { refreshToken: "" },
    );
    return toCookieSession(response.data);
  }

  async beginOidcAuthorization(
    slug: string,
    returnUrl: string,
    linkAccount: boolean,
  ): Promise<string> {
    const response = await this.axiosInstance.post<OidcAuthorizationResponse>(
      `/api/v1/auth/oidc/${encodeURIComponent(slug)}/authorization-url`,
      { returnUrl, linkAccount },
    );
    return response.data.authorizationUrl;
  }

  async getCurrentUser(): Promise<CurrentUser> {
    const response = await this.axiosInstance.get<CurrentUser>(
      "/api/v1/auth/me",
    );
    return response.data;
  }

  async listExternalIdentities(): Promise<ExternalIdentity[]> {
    const response = await this.axiosInstance.get<ExternalIdentity[]>(
      "/api/v1/auth/external-identities",
    );
    return response.data;
  }

  async unlinkExternalIdentity(identityId: string): Promise<void> {
    await this.axiosInstance.delete(
      `/api/v1/auth/external-identities/${encodeURIComponent(identityId)}`,
    );
  }

  async getAuthenticationSettings(): Promise<AuthenticationSettings> {
    const response = await this.axiosInstance.get<AuthenticationSettings>(
      "/api/v1/admin/authentication",
    );
    return response.data;
  }

  async updateAuthenticationSettings(
    settings: AuthenticationSettings,
  ): Promise<AuthenticationSettings> {
    const response = await this.axiosInstance.put<AuthenticationSettings>(
      "/api/v1/admin/authentication",
      settings,
    );
    return response.data;
  }

  async listOidcProviders(): Promise<OidcProvider[]> {
    const response = await this.axiosInstance.get<OidcProvider[]>(
      "/api/v1/admin/authentication/oidc-providers",
    );
    return response.data;
  }

  async createOidcProvider(
    request: SaveOidcProviderRequest,
  ): Promise<OidcProvider> {
    const response = await this.axiosInstance.post<OidcProvider>(
      "/api/v1/admin/authentication/oidc-providers",
      request,
    );
    return response.data;
  }

  async updateOidcProvider(
    providerId: string,
    request: SaveOidcProviderRequest,
  ): Promise<OidcProvider> {
    const response = await this.axiosInstance.put<OidcProvider>(
      `/api/v1/admin/authentication/oidc-providers/${encodeURIComponent(providerId)}`,
      request,
    );
    return response.data;
  }

  async deleteOidcProvider(providerId: string): Promise<void> {
    await this.axiosInstance.delete(
      `/api/v1/admin/authentication/oidc-providers/${encodeURIComponent(providerId)}`,
    );
  }

  async listUsers(): Promise<AdminUser[]> {
    const response = await this.axiosInstance.get<AdminUser[]>(
      "/api/v1/admin/users",
    );
    return response.data;
  }

  async createUser(request: CreateUserRequest): Promise<AdminUser> {
    const response = await this.axiosInstance.post<AdminUser>(
      "/api/v1/admin/users",
      request,
    );
    return response.data;
  }

  async updateUserAccess(
    userId: string,
    request: UpdateUserAccessRequest,
  ): Promise<AdminUser> {
    const response = await this.axiosInstance.put<AdminUser>(
      `/api/v1/admin/users/${encodeURIComponent(userId)}/access`,
      request,
    );
    return response.data;
  }
}

export function useAuthApi(): AuthApiClient {
  const axiosInstance = useAxios();
  return useMemo(() => new AuthApiClient(axiosInstance), [axiosInstance]);
}
