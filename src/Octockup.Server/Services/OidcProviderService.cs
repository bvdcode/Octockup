// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Requests;
using System.Text.RegularExpressions;

namespace Octockup.Server.Services
{
    public partial class OidcProviderService(
        AppDbContext _dbContext,
        IStreamCipher _cipher)
    {
        private const int MaxSlugLength = 64;
        private const string CallbackPath = "/api/v1/auth/oidc/callback";
        private static readonly string[] DefaultScopes = ["openid", "profile", "email"];

        public async Task<IReadOnlyList<PublicOidcProviderDto>> ListPublicAsync(
            CancellationToken cancellationToken)
        {
            List<PublicOidcProviderDto> providers = await _dbContext.OidcProviders
                .AsNoTracking()
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.Name)
                .Select(x => new PublicOidcProviderDto
                {
                    Name = x.Name,
                    Slug = x.Slug,
                })
                .ToListAsync(cancellationToken);

            return providers;
        }

        public async Task<IReadOnlyList<OidcProviderDto>> ListAdminAsync(
            CancellationToken cancellationToken)
        {
            List<OidcProvider> providers = await _dbContext.OidcProviders
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return providers.Select(ToDto).ToArray();
        }

        public async Task<OidcProviderDto> CreateAsync(
            OidcProviderRequest request,
            CancellationToken cancellationToken)
        {
            NormalizedOidcProviderInput input = Normalize(request);
            string slug = await ResolveSlugAsync(input.Slug, input.Name, null, cancellationToken);
            string? encryptedSecret = Encrypt(input.ClientSecret);
            if (request.ClearClientSecret)
            {
                encryptedSecret = null;
            }
            OidcProvider provider = new()
            {
                Name = input.Name,
                Slug = slug,
                Issuer = input.Issuer,
                PublicBaseUrl = input.PublicBaseUrl,
                ClientId = input.ClientId,
                ClientSecretEncrypted = encryptedSecret,
                Scopes = input.Scopes,
                IsEnabled = input.IsEnabled,
            };

            await _dbContext.OidcProviders.AddAsync(provider, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(provider);
        }

        public Task<OidcProviderDto> UpdateAsync(
            Guid providerId,
            OidcProviderRequest request,
            CancellationToken cancellationToken)
        {
            return AuthMutationTransaction.ExecuteAsync(
                _dbContext,
                () => UpdateCoreAsync(providerId, request, cancellationToken),
                cancellationToken);
        }

        private async Task<OidcProviderDto> UpdateCoreAsync(
            Guid providerId,
            OidcProviderRequest request,
            CancellationToken cancellationToken)
        {
            OidcProvider provider = await _dbContext.OidcProviders.FindAsync([providerId], cancellationToken)
                ?? throw new AuthApiException(StatusCodes.Status404NotFound, "OIDC provider was not found.");
            NormalizedOidcProviderInput input = Normalize(request);
            bool hasLinkedIdentities = await _dbContext.UserExternalIdentities
                .AnyAsync(x => x.ProviderId == providerId, cancellationToken);

            if (hasLinkedIdentities
                && (!string.Equals(provider.Issuer, input.Issuer, StringComparison.Ordinal)
                    || !string.Equals(provider.ClientId, input.ClientId, StringComparison.Ordinal)))
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "Create a new provider to change issuer or client id after accounts have been linked.");
            }

            if (provider.IsEnabled && !input.IsEnabled)
            {
                await EnsureCanDisableAsync(provider.Id, cancellationToken);
            }

            provider.Name = input.Name;
            provider.Slug = await ResolveSlugAsync(input.Slug, input.Name, provider.Id, cancellationToken);
            provider.Issuer = input.Issuer;
            provider.PublicBaseUrl = input.PublicBaseUrl;
            provider.ClientId = input.ClientId;
            if (request.ClearClientSecret)
            {
                provider.ClientSecretEncrypted = null;
            }
            else if (input.ClientSecret is not null)
            {
                provider.ClientSecretEncrypted = Encrypt(input.ClientSecret);
            }
            provider.Scopes = input.Scopes;
            provider.IsEnabled = input.IsEnabled;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(provider);
        }

        public async Task DeleteAsync(Guid providerId, CancellationToken cancellationToken)
        {
            OidcProvider provider = await _dbContext.OidcProviders.FindAsync([providerId], cancellationToken)
                ?? throw new AuthApiException(StatusCodes.Status404NotFound, "OIDC provider was not found.");
            bool hasLinkedIdentities = await _dbContext.UserExternalIdentities
                .AnyAsync(x => x.ProviderId == providerId, cancellationToken);
            if (hasLinkedIdentities)
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "Unlink every account from this provider before deleting it.");
            }

            List<OidcLoginState> loginStates = await _dbContext.OidcLoginStates
                .Where(x => x.ProviderId == providerId)
                .ToListAsync(cancellationToken);
            _dbContext.OidcLoginStates.RemoveRange(loginStates);
            _dbContext.OidcProviders.Remove(provider);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public string? DecryptClientSecret(OidcProvider provider)
        {
            if (string.IsNullOrWhiteSpace(provider.ClientSecretEncrypted))
            {
                return null;
            }

            byte[] encrypted = Convert.FromBase64String(provider.ClientSecretEncrypted);
            return _cipher.DecryptString(encrypted);
        }

        public static string GetCallbackUrl(OidcProvider provider)
        {
            return provider.PublicBaseUrl + CallbackPath;
        }

        private async Task EnsureCanDisableAsync(Guid providerId, CancellationToken cancellationToken)
        {
            bool passwordLoginEnabled = await _dbContext.AuthenticationSettings
                .Where(x => x.Name == AuthenticationSettings.GlobalName)
                .Select(x => (bool?)x.PasswordLoginEnabled)
                .SingleOrDefaultAsync(cancellationToken) ?? true;
            if (passwordLoginEnabled)
            {
                return;
            }

            bool wouldStrandUser = await _dbContext.Users
                .Where(x => !x.IsDisabled)
                .AnyAsync(
                    user => !_dbContext.UserExternalIdentities.Any(
                        identity => identity.UserId == user.Id
                            && identity.ProviderId != providerId
                            && identity.Provider.IsEnabled
                            && identity.Issuer == identity.Provider.Issuer),
                    cancellationToken);
            if (wouldStrandUser)
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "Enable password login or link another enabled provider for every active user first.");
            }
        }

        private async Task<string> ResolveSlugAsync(
            string? requestedSlug,
            string name,
            Guid? currentProviderId,
            CancellationToken cancellationToken)
        {
            string slug = requestedSlug is null ? Slugify(name) : NormalizeSlug(requestedSlug);
            bool exists = await _dbContext.OidcProviders.AnyAsync(
                x => x.Slug == slug && x.Id != currentProviderId,
                cancellationToken);
            if (exists)
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "OIDC provider slug is already used.");
            }

            return slug;
        }

        private string? Encrypt(string? value)
        {
            if (value is null)
            {
                return null;
            }

            return Convert.ToBase64String(_cipher.EncryptString(value));
        }

        private static NormalizedOidcProviderInput Normalize(OidcProviderRequest request)
        {
            string name = RequiredTrim(request.Name, "Provider name is required.", 80);
            string issuer = NormalizeUrl(request.Issuer, "Issuer", allowLoopbackHttp: true);
            string publicBaseUrl = NormalizeUrl(
                request.PublicBaseUrl,
                "Public base URL",
                allowLoopbackHttp: true);
            string clientId = RequiredTrim(request.ClientId, "Client id is required.", 256);
            string? clientSecret = string.IsNullOrWhiteSpace(request.ClientSecret)
                ? null
                : request.ClientSecret.Trim();
            string[] scopes = NormalizeScopes(request.Scopes);
            string? slug = string.IsNullOrWhiteSpace(request.Slug)
                ? null
                : NormalizeSlug(request.Slug);

            return new NormalizedOidcProviderInput(
                name,
                slug,
                issuer,
                publicBaseUrl,
                clientId,
                clientSecret,
                scopes,
                request.IsEnabled);
        }

        private static string RequiredTrim(string? value, string error, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AuthApiException(StatusCodes.Status400BadRequest, error);
            }
            string trimmed = value.Trim();
            if (trimmed.Length > maxLength)
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    $"Value cannot exceed {maxLength} characters.");
            }

            return trimmed;
        }

        private static string NormalizeUrl(string value, string name, bool allowLoopbackHttp)
        {
            string trimmed = RequiredTrim(value, $"{name} is required.", 512).TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
                || string.IsNullOrWhiteSpace(uri.Host)
                || !IsAllowedScheme(uri, allowLoopbackHttp)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    $"{name} must be an absolute HTTPS URL without a query or fragment.");
            }

            return trimmed;
        }

        private static bool IsAllowedScheme(Uri uri, bool allowLoopbackHttp)
        {
            if (uri.Scheme == Uri.UriSchemeHttps)
            {
                return true;
            }

            return allowLoopbackHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        }

        private static string[] NormalizeScopes(string[]? scopes)
        {
            string[] normalized = (scopes ?? [])
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (normalized.Length == 0)
            {
                normalized = DefaultScopes;
            }
            if (!normalized.Contains("openid", StringComparer.Ordinal))
            {
                normalized = ["openid", .. normalized];
            }

            return normalized;
        }

        private static string Slugify(string value)
        {
            string normalized = SlugInvalidCharacters()
                .Replace(value.Trim().ToLowerInvariant(), "-")
                .Trim('-');
            if (normalized.Length == 0 || normalized[0] is < 'a' or > 'z')
            {
                normalized = "oidc-" + normalized;
            }
            if (normalized.Length < 2)
            {
                normalized += "-provider";
            }

            return normalized[..Math.Min(normalized.Length, MaxSlugLength)];
        }

        private static string NormalizeSlug(string value)
        {
            string slug = value.Trim().ToLowerInvariant();
            if (!SlugRegex().IsMatch(slug))
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "Slug must start with a letter and contain lowercase letters, digits, dots, dashes, or underscores.");
            }

            return slug;
        }

        private static OidcProviderDto ToDto(OidcProvider provider)
        {
            return new OidcProviderDto
            {
                Id = provider.Id,
                CreatedAt = provider.CreatedAt,
                UpdatedAt = provider.UpdatedAt,
                Name = provider.Name,
                Slug = provider.Slug,
                Issuer = provider.Issuer,
                PublicBaseUrl = provider.PublicBaseUrl,
                CallbackUrl = GetCallbackUrl(provider),
                ClientId = provider.ClientId,
                HasClientSecret = !string.IsNullOrWhiteSpace(provider.ClientSecretEncrypted),
                Scopes = provider.Scopes,
                IsEnabled = provider.IsEnabled,
            };
        }

        [GeneratedRegex("[^a-z0-9._-]+", RegexOptions.CultureInvariant)]
        private static partial Regex SlugInvalidCharacters();

        [GeneratedRegex("^[a-z](?:[a-z0-9]|[._-](?=[a-z0-9])){1,63}$", RegexOptions.CultureInvariant)]
        private static partial Regex SlugRegex();
    }
}
