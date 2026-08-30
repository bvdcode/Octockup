// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models;

namespace Octockup.Server.Services
{
    public partial class OidcAuthenticationService
    {
        private async Task<User> LinkIdentityAsync(
            Guid userId,
            OidcProvider provider,
            OidcIdentityClaims claims,
            CancellationToken cancellationToken)
        {
            return await AuthMutationTransaction.ExecuteAsync(
                _dbContext,
                () => LinkIdentityCoreAsync(userId, provider, claims, cancellationToken),
                cancellationToken);
        }

        private async Task<User> LinkIdentityCoreAsync(
            Guid userId,
            OidcProvider expectedProvider,
            OidcIdentityClaims claims,
            CancellationToken cancellationToken)
        {
            OidcProvider provider = await GetCurrentLinkProviderAsync(
                expectedProvider,
                cancellationToken);
            User user = await GetAvailableUserAsync(userId, cancellationToken);
            await EnsureSubjectIsAvailableAsync(provider.Id, claims.Subject, userId, cancellationToken);

            UserExternalIdentity? providerLink = await _dbContext.UserExternalIdentities
                .SingleOrDefaultAsync(
                    x => x.ProviderId == provider.Id && x.UserId == userId,
                    cancellationToken);
            if (providerLink is not null)
            {
                EnsureProviderLinkMatches(providerLink, claims.Subject);
                ApplyClaims(providerLink, claims);
            }
            else
            {
                UserExternalIdentity identity = CreateIdentity(userId, provider, claims);
                await _dbContext.UserExternalIdentities.AddAsync(identity, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return user;
        }

        private async Task<OidcProvider> GetCurrentLinkProviderAsync(
            OidcProvider expectedProvider,
            CancellationToken cancellationToken)
        {
            OidcProvider provider = await _dbContext.OidcProviders
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == expectedProvider.Id, cancellationToken)
                ?? throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "OIDC provider changed while the account was being linked.");
            if (provider.IsEnabled
                && string.Equals(provider.Issuer, expectedProvider.Issuer, StringComparison.Ordinal)
                && string.Equals(provider.ClientId, expectedProvider.ClientId, StringComparison.Ordinal))
            {
                return provider;
            }

            throw new AuthApiException(
                StatusCodes.Status409Conflict,
                "OIDC provider changed while the account was being linked. Try again.");
        }

        private async Task<User> GetAvailableUserAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            User user = await _dbContext.Users.FindAsync([userId], cancellationToken)
                ?? throw new AuthApiException(StatusCodes.Status404NotFound, "User was not found.");
            if (user.IsDisabled)
            {
                throw new AuthApiException(StatusCodes.Status403Forbidden, "User account is unavailable.");
            }

            return user;
        }

        private async Task EnsureSubjectIsAvailableAsync(
            Guid providerId,
            string subject,
            Guid userId,
            CancellationToken cancellationToken)
        {
            UserExternalIdentity? subjectLink = await _dbContext.UserExternalIdentities
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.ProviderId == providerId && x.Subject == subject,
                    cancellationToken);
            if (subjectLink is null || subjectLink.UserId == userId)
            {
                return;
            }

            throw new AuthApiException(
                StatusCodes.Status409Conflict,
                "This external account is already linked to another user.");
        }

        private static void EnsureProviderLinkMatches(
            UserExternalIdentity providerLink,
            string subject)
        {
            if (string.Equals(providerLink.Subject, subject, StringComparison.Ordinal))
            {
                return;
            }

            throw new AuthApiException(
                StatusCodes.Status409Conflict,
                "This user is already linked to another account from the same provider.");
        }

        private static UserExternalIdentity CreateIdentity(
            Guid userId,
            OidcProvider provider,
            OidcIdentityClaims claims)
        {
            UserExternalIdentity identity = new()
            {
                UserId = userId,
                ProviderId = provider.Id,
                Issuer = provider.Issuer,
                Subject = claims.Subject,
            };
            ApplyClaims(identity, claims);
            return identity;
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
    }
}
