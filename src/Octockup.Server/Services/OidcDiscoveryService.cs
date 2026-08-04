// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Octockup.Server.Database;
using Octockup.Server.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Octockup.Server.Services
{
    public class OidcDiscoveryService(
        HttpClient _httpClient,
        ILogger<OidcDiscoveryService> _logger)
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
            OidcProvider provider,
            CancellationToken cancellationToken)
        {
            string metadataAddress = provider.Issuer.TrimEnd('/') + "/.well-known/openid-configuration";
            HttpDocumentRetriever retriever = new(_httpClient)
            {
                RequireHttps = !IsLoopbackHttp(provider.Issuer),
            };

            try
            {
                OpenIdConnectConfiguration configuration = await OpenIdConnectConfigurationRetriever.GetAsync(
                    metadataAddress,
                    retriever,
                    cancellationToken);
                ValidateConfiguration(provider, configuration);
                return configuration;
            }
            catch (AuthApiException)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "OIDC discovery failed for provider {ProviderId}",
                    provider.Id);
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "OIDC discovery document could not be loaded.");
            }
        }

        public async Task<OidcTokenResponse> ExchangeCodeAsync(
            OpenIdConnectConfiguration configuration,
            OidcProvider provider,
            string? clientSecret,
            string code,
            string redirectUri,
            string codeVerifier,
            CancellationToken cancellationToken)
        {
            string tokenEndpoint = RequireEndpoint(configuration.TokenEndpoint, "token", provider.Issuer);
            List<KeyValuePair<string, string>> formValues =
            [
                new("grant_type", "authorization_code"),
                new("code", code),
                new("redirect_uri", redirectUri),
                new("code_verifier", codeVerifier),
            ];

            using HttpRequestMessage request = new(HttpMethod.Post, tokenEndpoint);
            if (clientSecret is null)
            {
                formValues.Add(new KeyValuePair<string, string>("client_id", provider.ClientId));
            }
            else if (SupportsClientSecretBasic(configuration))
            {
                string credentials = Uri.EscapeDataString(provider.ClientId)
                    + ":"
                    + Uri.EscapeDataString(clientSecret);
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
            }
            else if (configuration.TokenEndpointAuthMethodsSupported.Contains(
                "client_secret_post",
                StringComparer.Ordinal))
            {
                formValues.Add(new KeyValuePair<string, string>("client_id", provider.ClientId));
                formValues.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
            }
            else
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "OIDC provider does not support client secret authentication compatible with Octockup.");
            }

            request.Content = new FormUrlEncodedContent(formValues);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OIDC token exchange failed for provider {ProviderId} with status {StatusCode}",
                    provider.Id,
                    response.StatusCode);
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "OIDC token exchange failed.");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            OidcTokenResponse? result = await JsonSerializer.DeserializeAsync<OidcTokenResponse>(
                stream,
                JsonOptions,
                cancellationToken);
            if (result is null || string.IsNullOrWhiteSpace(result.IdToken))
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "OIDC token response did not include an ID token.");
            }

            return result;
        }

        private static bool SupportsClientSecretBasic(OpenIdConnectConfiguration configuration)
        {
            return configuration.TokenEndpointAuthMethodsSupported.Count == 0
                || configuration.TokenEndpointAuthMethodsSupported.Contains(
                    "client_secret_basic",
                    StringComparer.Ordinal);
        }

        public string GetAuthorizationEndpoint(
            OpenIdConnectConfiguration configuration,
            OidcProvider provider)
        {
            return RequireEndpoint(configuration.AuthorizationEndpoint, "authorization", provider.Issuer);
        }

        private static void ValidateConfiguration(
            OidcProvider provider,
            OpenIdConnectConfiguration configuration)
        {
            string discoveredIssuer = configuration.Issuer?.TrimEnd('/') ?? string.Empty;
            if (!string.Equals(discoveredIssuer, provider.Issuer, StringComparison.Ordinal))
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "OIDC discovery issuer does not match the configured issuer.");
            }
            if (configuration.SigningKeys.Count == 0)
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "OIDC provider does not publish signing keys.");
            }
        }

        private static string RequireEndpoint(string? endpoint, string name, string issuer)
        {
            if (string.IsNullOrWhiteSpace(endpoint)
                || !Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                    && !(IsLoopbackHttp(endpoint) && IsLoopbackHttp(issuer))))
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    $"OIDC provider does not publish a secure {name} endpoint.");
            }

            return endpoint;
        }

        private static bool IsLoopbackHttp(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttp
                && uri.IsLoopback;
        }
    }
}
