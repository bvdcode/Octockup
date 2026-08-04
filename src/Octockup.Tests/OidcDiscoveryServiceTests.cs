// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Octockup.Server.Database;
using Octockup.Server.Services;
using System.Text;

namespace Octockup.Tests
{
    public class OidcDiscoveryServiceTests
    {
        [Test]
        public async Task ExchangeCode_WhenMetadataOmitsAuthMethods_UsesClientSecretBasic()
        {
            using System.Security.Cryptography.RSA rsa = System.Security.Cryptography.RSA.Create(2048);
            using OidcTestHttpMessageHandler handler = new(rsa)
            {
                IdToken = "id-token",
            };
            using HttpClient httpClient = new(handler);
            OidcDiscoveryService service = new(
                httpClient,
                NullLogger<OidcDiscoveryService>.Instance);
            OidcProvider provider = CreateProvider();
            OpenIdConnectConfiguration configuration = await service.GetConfigurationAsync(
                provider,
                CancellationToken.None);

            await service.ExchangeCodeAsync(
                configuration,
                provider,
                "client-secret",
                "code",
                "https://octockup.example/api/v1/auth/oidc/callback",
                "verifier",
                CancellationToken.None);

            string expectedCredentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes("client-id:client-secret"));
            Assert.That(handler.LastTokenAuthorization?.Scheme, Is.EqualTo("Basic"));
            Assert.That(handler.LastTokenAuthorization?.Parameter, Is.EqualTo(expectedCredentials));
            Assert.That(handler.LastTokenBody, Does.Not.Contain("client_secret="));
        }

        [Test]
        public async Task ExchangeCode_WhenProviderAdvertisesPost_UsesClientSecretPost()
        {
            using System.Security.Cryptography.RSA rsa = System.Security.Cryptography.RSA.Create(2048);
            using OidcTestHttpMessageHandler handler = new(rsa)
            {
                IdToken = "id-token",
                TokenEndpointAuthMethodsSupported = ["client_secret_post"],
            };
            using HttpClient httpClient = new(handler);
            OidcDiscoveryService service = new(
                httpClient,
                NullLogger<OidcDiscoveryService>.Instance);
            OidcProvider provider = CreateProvider();
            OpenIdConnectConfiguration configuration = await service.GetConfigurationAsync(
                provider,
                CancellationToken.None);

            await service.ExchangeCodeAsync(
                configuration,
                provider,
                "client-secret",
                "code",
                "https://octockup.example/api/v1/auth/oidc/callback",
                "verifier",
                CancellationToken.None);

            Assert.That(handler.LastTokenAuthorization, Is.Null);
            Assert.That(handler.LastTokenBody, Does.Contain("client_id=client-id"));
            Assert.That(handler.LastTokenBody, Does.Contain("client_secret=client-secret"));
        }

        private static OidcProvider CreateProvider()
        {
            return new OidcProvider
            {
                Name = "Provider",
                Slug = "provider",
                Issuer = "https://issuer.example",
                PublicBaseUrl = "https://octockup.example",
                ClientId = "client-id",
                Scopes = ["openid"],
                IsEnabled = true,
            };
        }
    }
}
