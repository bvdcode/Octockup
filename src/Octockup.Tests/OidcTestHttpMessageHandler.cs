// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Octockup.Tests
{
    internal class OidcTestHttpMessageHandler(RSA _rsa) : HttpMessageHandler
    {
        public string? IdToken { get; set; }
        public string[] TokenEndpointAuthMethodsSupported { get; set; } = [];
        public AuthenticationHeaderValue? LastTokenAuthorization { get; private set; }
        public string? LastTokenBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri requestUri = request.RequestUri
                ?? throw new InvalidOperationException("Request URI is required.");
            string issuer = requestUri.GetLeftPart(UriPartial.Authority);
            if (requestUri.AbsolutePath == "/.well-known/openid-configuration")
            {
                return JsonResponse(new
                {
                    issuer = issuer + "/",
                    authorization_endpoint = issuer + "/authorize",
                    token_endpoint = issuer + "/token",
                    jwks_uri = issuer + "/jwks",
                    response_types_supported = new[] { "code" },
                    subject_types_supported = new[] { "public" },
                    id_token_signing_alg_values_supported = new[] { "RS256" },
                    token_endpoint_auth_methods_supported = TokenEndpointAuthMethodsSupported,
                });
            }
            if (requestUri.AbsolutePath == "/jwks")
            {
                RSAParameters parameters = _rsa.ExportParameters(false);
                return JsonResponse(new
                {
                    keys = new[]
                    {
                        new
                        {
                            kty = "RSA",
                            use = "sig",
                            kid = "test-key",
                            alg = "RS256",
                            n = WebEncoders.Base64UrlEncode(parameters.Modulus!),
                            e = WebEncoders.Base64UrlEncode(parameters.Exponent!),
                        },
                    },
                });
            }
            if (requestUri.AbsolutePath == "/token" && request.Method == HttpMethod.Post)
            {
                if (IdToken is null)
                {
                    throw new InvalidOperationException("ID token was not configured.");
                }

                LastTokenAuthorization = request.Headers.Authorization;
                LastTokenBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                return JsonResponse(new
                {
                    id_token = IdToken,
                    access_token = "provider-access-token",
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage JsonResponse<T>(T value)
        {
            string json = JsonSerializer.Serialize(value);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
