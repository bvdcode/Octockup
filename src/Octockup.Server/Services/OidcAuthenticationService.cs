// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Octockup.Server.Services
{
    public class OidcAuthenticationService(
        AppDbContext _dbContext,
        OidcDiscoveryService _discovery,
        OidcProviderService _providers,
        AuthenticationSettingsService _authenticationSettings,
        IAuthSessionIssuer _sessionIssuer,
        IStreamCipher _cipher,
        ILogger<OidcAuthenticationService> _logger)
    {
        private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);
        private const string CodeChallengeMethod = "S256";
        private const string CorrelationCookiePrefix = "octockup_oidc_";
        private const string CallbackPath = "/api/v1/auth/oidc/callback";

        public Task<string> BeginSignInAsync(
            string providerSlug,
            string? returnUrl,
            HttpResponse response,
            CancellationToken cancellationToken)
        {
            return BeginAsync(
                providerSlug,
                NormalizeReturnUrl(returnUrl, "/login"),
                null,
                response,
                cancellationToken);
        }

        public async Task<string> BeginLinkAsync(
            Guid userId,
            string providerSlug,
            string? returnUrl,
            HttpResponse response,
            CancellationToken cancellationToken)
        {
            bool userAvailable = await _dbContext.Users
                .AnyAsync(x => x.Id == userId && !x.IsDisabled, cancellationToken);
            if (!userAvailable)
            {
                throw new AuthApiException(StatusCodes.Status403Forbidden, "User account is unavailable.");
            }

            return await BeginAsync(
                providerSlug,
                NormalizeReturnUrl(returnUrl, "/settings"),
                userId,
                response,
                cancellationToken);
        }

        public async Task<string> CompleteCallbackAsync(
            string state,
            string code,
            HttpRequest request,
            HttpResponse response,
            CancellationToken cancellationToken)
        {
            RequireCorrelation(state, request);
            DeleteCorrelationCookie(state, response);
            string stateHash = HashOpaqueValue(state);
            OidcLoginState loginState = await _dbContext.OidcLoginStates
                .Include(x => x.Provider)
                .SingleOrDefaultAsync(x => x.StateHash == stateHash, cancellationToken)
                ?? throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "OIDC sign-in state was not found.");

            try
            {
                return await CompleteCallbackCoreAsync(
                    loginState,
                    code,
                    response,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                string errorReturnUrl = QueryHelpers.AddQueryString(
                    loginState.ReturnUrl,
                    "oidc",
                    "error");
                throw new OidcCallbackException(errorReturnUrl, exception);
            }
        }

        private async Task<string> CompleteCallbackCoreAsync(
            OidcLoginState loginState,
            string code,
            HttpResponse response,
            CancellationToken cancellationToken)
        {

            _dbContext.OidcLoginStates.Remove(loginState);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (DateTime.UtcNow > loginState.ExpiresAt)
            {
                throw new AuthApiException(StatusCodes.Status400BadRequest, "OIDC sign-in state has expired.");
            }
            if (!loginState.Provider.IsEnabled)
            {
                throw new AuthApiException(StatusCodes.Status400BadRequest, "OIDC provider is disabled.");
            }

            OpenIdConnectConfiguration configuration = await _discovery.GetConfigurationAsync(
                loginState.Provider,
                cancellationToken);
            string redirectUri = OidcProviderService.GetCallbackUrl(loginState.Provider);
            string codeVerifier = Decrypt(loginState.CodeVerifierEncrypted);
            string nonce = Decrypt(loginState.NonceEncrypted);
            string? clientSecret = _providers.DecryptClientSecret(loginState.Provider);
            OidcTokenResponse tokenResponse = await _discovery.ExchangeCodeAsync(
                configuration,
                loginState.Provider,
                clientSecret,
                code,
                redirectUri,
                codeVerifier,
                cancellationToken);
            ClaimsPrincipal principal = ValidateIdToken(
                configuration,
                loginState.Provider,
                tokenResponse.IdToken,
                nonce);
            OidcIdentityClaims claims = ReadIdentityClaims(principal);

            string marker;
            if (loginState.LinkUserId is Guid linkUserId)
            {
                await LinkIdentityAsync(linkUserId, loginState.Provider, claims, cancellationToken);
                marker = "linked";
            }
            else
            {
                User user = await ResolveSignInUserAsync(
                    loginState.Provider,
                    claims,
                    cancellationToken);
                await _sessionIssuer.IssueAsync(user, response, cancellationToken);
                marker = "success";
            }

            return QueryHelpers.AddQueryString(loginState.ReturnUrl, "oidc", marker);
        }

        public async Task<string> CancelCallbackAsync(
            string? state,
            HttpRequest request,
            HttpResponse response,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return "/login?oidc=error";
            }

            string normalizedState = state.Trim();
            RequireCorrelation(normalizedState, request);
            DeleteCorrelationCookie(normalizedState, response);
            string stateHash = HashOpaqueValue(normalizedState);
            OidcLoginState? loginState = await _dbContext.OidcLoginStates
                .SingleOrDefaultAsync(x => x.StateHash == stateHash, cancellationToken);
            if (loginState is null)
            {
                return "/login?oidc=error";
            }

            _dbContext.OidcLoginStates.Remove(loginState);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return QueryHelpers.AddQueryString(loginState.ReturnUrl, "oidc", "error");
        }

        public async Task<IReadOnlyList<UserExternalIdentityDto>> ListLinkedAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            List<UserExternalIdentityDto> identities = await _dbContext.UserExternalIdentities
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Provider.Name)
                .Select(x => new UserExternalIdentityDto
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    ProviderId = x.ProviderId,
                    ProviderName = x.Provider.Name,
                    ProviderSlug = x.Provider.Slug,
                    ProviderEnabled = x.Provider.IsEnabled,
                    Email = x.Email,
                    DisplayName = x.DisplayName,
                    LastUsedAt = x.LastUsedAt,
                })
                .ToListAsync(cancellationToken);

            return identities;
        }

        public async Task UnlinkAsync(
            Guid userId,
            Guid identityId,
            CancellationToken cancellationToken)
        {
            await AuthMutationTransaction.ExecuteAsync(
                _dbContext,
                async () =>
                {
                    await UnlinkCoreAsync(userId, identityId, cancellationToken);
                    return true;
                },
                cancellationToken);
        }

        private async Task UnlinkCoreAsync(
            Guid userId,
            Guid identityId,
            CancellationToken cancellationToken)
        {
            UserExternalIdentity identity = await _dbContext.UserExternalIdentities
                .SingleOrDefaultAsync(
                    x => x.Id == identityId && x.UserId == userId,
                    cancellationToken)
                ?? throw new AuthApiException(
                    StatusCodes.Status404NotFound,
                    "External identity was not found.");
            await _authenticationSettings.EnsureCanUnlinkAsync(
                userId,
                identityId,
                cancellationToken);
            _dbContext.UserExternalIdentities.Remove(identity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<string> BeginAsync(
            string providerSlug,
            string returnUrl,
            Guid? linkUserId,
            HttpResponse response,
            CancellationToken cancellationToken)
        {
            await CleanupExpiredStatesAsync(cancellationToken);
            string normalizedSlug = providerSlug.Trim().ToLowerInvariant();
            OidcProvider provider = await _dbContext.OidcProviders
                .SingleOrDefaultAsync(x => x.Slug == normalizedSlug, cancellationToken)
                ?? throw new AuthApiException(StatusCodes.Status404NotFound, "OIDC provider was not found.");
            if (!provider.IsEnabled)
            {
                throw new AuthApiException(StatusCodes.Status400BadRequest, "OIDC provider is disabled.");
            }

            OpenIdConnectConfiguration configuration = await _discovery.GetConfigurationAsync(
                provider,
                cancellationToken);
            string authorizationEndpoint = _discovery.GetAuthorizationEndpoint(configuration, provider);
            string state = CreateOpaqueValue();
            string codeVerifier = CreateOpaqueValue();
            string nonce = CreateOpaqueValue();
            OidcLoginState loginState = new()
            {
                ProviderId = provider.Id,
                StateHash = HashOpaqueValue(state),
                CodeVerifierEncrypted = Encrypt(codeVerifier),
                NonceEncrypted = Encrypt(nonce),
                ReturnUrl = returnUrl,
                LinkUserId = linkUserId,
                ExpiresAt = DateTime.UtcNow.Add(StateLifetime),
            };
            await _dbContext.OidcLoginStates.AddAsync(loginState, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            response.Cookies.Append(
                GetCorrelationCookieName(state),
                state,
                CreateCorrelationCookieOptions());

            Dictionary<string, string?> parameters = new()
            {
                ["response_type"] = OpenIdConnectResponseType.Code,
                ["client_id"] = provider.ClientId,
                ["redirect_uri"] = OidcProviderService.GetCallbackUrl(provider),
                ["scope"] = string.Join(' ', provider.Scopes),
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = CreateCodeChallenge(codeVerifier),
                ["code_challenge_method"] = CodeChallengeMethod,
            };
            return QueryHelpers.AddQueryString(authorizationEndpoint, parameters);
        }

        private async Task LinkIdentityAsync(
            Guid userId,
            OidcProvider provider,
            OidcIdentityClaims claims,
            CancellationToken cancellationToken)
        {
            await AuthMutationTransaction.ExecuteAsync(
                _dbContext,
                async () =>
                {
                    await LinkIdentityCoreAsync(userId, provider, claims, cancellationToken);
                    return true;
                },
                cancellationToken);
        }

        private async Task LinkIdentityCoreAsync(
            Guid userId,
            OidcProvider expectedProvider,
            OidcIdentityClaims claims,
            CancellationToken cancellationToken)
        {
            OidcProvider provider = await _dbContext.OidcProviders
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == expectedProvider.Id, cancellationToken)
                ?? throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "OIDC provider changed while the account was being linked.");
            if (!provider.IsEnabled
                || !string.Equals(provider.Issuer, expectedProvider.Issuer, StringComparison.Ordinal)
                || !string.Equals(provider.ClientId, expectedProvider.ClientId, StringComparison.Ordinal))
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "OIDC provider changed while the account was being linked. Try again.");
            }

            User user = await _dbContext.Users.FindAsync([userId], cancellationToken)
                ?? throw new AuthApiException(StatusCodes.Status404NotFound, "User was not found.");
            if (user.IsDisabled)
            {
                throw new AuthApiException(StatusCodes.Status403Forbidden, "User account is unavailable.");
            }

            UserExternalIdentity? subjectLink = await _dbContext.UserExternalIdentities
                .SingleOrDefaultAsync(
                    x => x.ProviderId == provider.Id && x.Subject == claims.Subject,
                    cancellationToken);
            if (subjectLink is not null && subjectLink.UserId != userId)
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "This external account is already linked to another user.");
            }

            UserExternalIdentity? providerLink = await _dbContext.UserExternalIdentities
                .SingleOrDefaultAsync(
                    x => x.ProviderId == provider.Id && x.UserId == userId,
                    cancellationToken);
            if (providerLink is not null)
            {
                if (!string.Equals(providerLink.Subject, claims.Subject, StringComparison.Ordinal))
                {
                    throw new AuthApiException(
                        StatusCodes.Status409Conflict,
                        "This user is already linked to another account from the same provider.");
                }

                ApplyClaims(providerLink, claims);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            UserExternalIdentity identity = new()
            {
                UserId = userId,
                ProviderId = provider.Id,
                Issuer = provider.Issuer,
                Subject = claims.Subject,
            };
            ApplyClaims(identity, claims);
            await _dbContext.UserExternalIdentities.AddAsync(identity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<User> ResolveSignInUserAsync(
            OidcProvider provider,
            OidcIdentityClaims claims,
            CancellationToken cancellationToken)
        {
            UserExternalIdentity? identity = await _dbContext.UserExternalIdentities
                .Include(x => x.User)
                .SingleOrDefaultAsync(
                    x => x.ProviderId == provider.Id && x.Subject == claims.Subject,
                    cancellationToken);
            if (identity is null)
            {
                throw new AuthApiException(
                    StatusCodes.Status403Forbidden,
                    "Sign in with your password and link this external account first.");
            }
            if (!string.Equals(identity.Issuer, provider.Issuer, StringComparison.Ordinal))
            {
                throw new AuthApiException(
                    StatusCodes.Status403Forbidden,
                    "External identity issuer does not match the configured provider.");
            }
            if (identity.User.IsDisabled)
            {
                throw new AuthApiException(StatusCodes.Status403Forbidden, "User account is unavailable.");
            }

            ApplyClaims(identity, claims);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return identity.User;
        }

        private ClaimsPrincipal ValidateIdToken(
            OpenIdConnectConfiguration configuration,
            OidcProvider provider,
            string idToken,
            string nonce)
        {
            JwtSecurityTokenHandler handler = new()
            {
                MapInboundClaims = false,
            };
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = configuration.Issuer ?? provider.Issuer,
                ValidateAudience = true,
                ValidAudience = provider.ClientId,
                ValidateLifetime = true,
                ClockSkew = ClockSkew,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                NameClaimType = "name",
            };
            if (configuration.IdTokenSigningAlgValuesSupported.Count > 0)
            {
                validationParameters.ValidAlgorithms = configuration.IdTokenSigningAlgValuesSupported;
            }

            try
            {
                ClaimsPrincipal principal = handler.ValidateToken(
                    idToken,
                    validationParameters,
                    out SecurityToken validatedToken);
                if (validatedToken is not JwtSecurityToken jwt
                    || string.Equals(jwt.Header.Alg, SecurityAlgorithms.None, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityTokenValidationException("Unsigned ID token.");
                }

                string? tokenNonce = FindClaim(principal, "nonce");
                if (!string.Equals(tokenNonce, nonce, StringComparison.Ordinal))
                {
                    throw new SecurityTokenValidationException("Invalid nonce.");
                }

                List<string> audiences = jwt.Audiences.ToList();
                string? authorizedParty = FindClaim(principal, "azp");
                if ((audiences.Count > 1 || authorizedParty is not null)
                    && !string.Equals(authorizedParty, provider.ClientId, StringComparison.Ordinal))
                {
                    throw new SecurityTokenValidationException("Invalid authorized party.");
                }

                return principal;
            }
            catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
            {
                _logger.LogWarning(
                    exception,
                    "OIDC ID token validation failed for provider {ProviderId}",
                    provider.Id);
                throw new AuthApiException(StatusCodes.Status400BadRequest, "OIDC ID token is invalid.");
            }
        }

        private static OidcIdentityClaims ReadIdentityClaims(ClaimsPrincipal principal)
        {
            string subject = RequiredClaim(principal, JwtRegisteredClaimNames.Sub, 256);
            string? email = OptionalClaim(principal, 320, JwtRegisteredClaimNames.Email, "email");
            string? displayName = OptionalClaim(principal, 160, "name", "preferred_username");
            return new OidcIdentityClaims(subject, email, displayName);
        }

        private static string RequiredClaim(ClaimsPrincipal principal, string type, int maxLength)
        {
            string? value = FindClaim(principal, type)?.Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    $"OIDC {type} claim is missing or invalid.");
            }

            return value;
        }

        private static string? OptionalClaim(
            ClaimsPrincipal principal,
            int maxLength,
            params string[] types)
        {
            foreach (string type in types)
            {
                string? value = FindClaim(principal, type)?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }
                if (value.Length > maxLength)
                {
                    throw new AuthApiException(
                        StatusCodes.Status400BadRequest,
                        $"OIDC {type} claim is too long.");
                }

                return value;
            }

            return null;
        }

        private static string? FindClaim(ClaimsPrincipal principal, string type)
        {
            return principal.FindFirst(type)?.Value;
        }

        private static void ApplyClaims(UserExternalIdentity identity, OidcIdentityClaims claims)
        {
            identity.Email = claims.Email;
            identity.DisplayName = claims.DisplayName;
            identity.LastUsedAt = DateTime.UtcNow;
        }

        private Task CleanupExpiredStatesAsync(CancellationToken cancellationToken)
        {
            return _dbContext.OidcLoginStates
                .Where(x => x.ExpiresAt < DateTime.UtcNow)
                .ExecuteDeleteAsync(cancellationToken);
        }

        private string Encrypt(string value)
        {
            return Convert.ToBase64String(_cipher.EncryptString(value));
        }

        private string Decrypt(string value)
        {
            return _cipher.DecryptString(Convert.FromBase64String(value));
        }

        internal static string NormalizeReturnUrl(string? returnUrl, string defaultReturnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return defaultReturnUrl;
            }

            string trimmed = returnUrl.Trim();
            if (trimmed.Length > 1024)
            {
                return defaultReturnUrl;
            }
            string decoded = Uri.UnescapeDataString(trimmed);
            if (!decoded.StartsWith('/')
                || decoded.StartsWith("//", StringComparison.Ordinal)
                || decoded.Contains('\\')
                || decoded.Any(char.IsControl))
            {
                return defaultReturnUrl;
            }

            return trimmed;
        }

        private static string CreateOpaqueValue()
        {
            return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        }

        private static string HashOpaqueValue(string value)
        {
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            return WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        }

        private static void RequireCorrelation(string state, HttpRequest request)
        {
            string cookieName = GetCorrelationCookieName(state);
            if (!request.Cookies.TryGetValue(cookieName, out string? correlation)
                || string.IsNullOrWhiteSpace(correlation)
                || !CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(Encoding.UTF8.GetBytes(state)),
                    SHA256.HashData(Encoding.UTF8.GetBytes(correlation))))
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "OIDC correlation cookie is missing or invalid.");
            }
        }

        private static void DeleteCorrelationCookie(string state, HttpResponse response)
        {
            response.Cookies.Delete(
                GetCorrelationCookieName(state),
                new CookieOptions
                {
                    Secure = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Path = CallbackPath,
                });
        }

        private static CookieOptions CreateCorrelationCookieOptions()
        {
            return new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = CallbackPath,
                MaxAge = StateLifetime,
                Expires = DateTimeOffset.UtcNow.Add(StateLifetime),
            };
        }

        private static string GetCorrelationCookieName(string state)
        {
            return CorrelationCookiePrefix + HashOpaqueValue(state);
        }
    }
}
