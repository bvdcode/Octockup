// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class AuthenticationSettingsService(AppDbContext _dbContext)
    {
        public async Task<bool> IsPasswordLoginEnabledAsync(CancellationToken cancellationToken)
        {
            bool? enabled = await _dbContext.AuthenticationSettings
                .AsNoTracking()
                .Where(x => x.Name == AuthenticationSettings.GlobalName)
                .Select(x => (bool?)x.PasswordLoginEnabled)
                .SingleOrDefaultAsync(cancellationToken);
            return enabled ?? true;
        }

        public async Task SetPasswordLoginEnabledAsync(bool enabled, CancellationToken cancellationToken)
        {
            await AuthMutationTransaction.ExecuteAsync(
                _dbContext,
                async () =>
                {
                    await SetPasswordLoginEnabledCoreAsync(enabled, cancellationToken);
                    return true;
                },
                cancellationToken);
        }

        private async Task SetPasswordLoginEnabledCoreAsync(
            bool enabled,
            CancellationToken cancellationToken)
        {
            if (!enabled)
            {
                bool hasActiveUsers = await _dbContext.Users
                    .AnyAsync(x => !x.IsDisabled, cancellationToken);
                bool hasUserWithoutExternalLogin = await _dbContext.Users
                    .Where(x => !x.IsDisabled)
                    .AnyAsync(
                        user => !user.ExternalIdentities.Any(
                            identity => identity.Provider.IsEnabled
                                && identity.Issuer == identity.Provider.Issuer),
                        cancellationToken);
                if (!hasActiveUsers || hasUserWithoutExternalLogin)
                {
                    throw new AuthApiException(
                        StatusCodes.Status409Conflict,
                        "Every active user must link an enabled external identity before password login can be disabled.");
                }
            }

            AuthenticationSettings? settings = await _dbContext.AuthenticationSettings
                .SingleOrDefaultAsync(x => x.Name == AuthenticationSettings.GlobalName, cancellationToken);
            if (settings == null)
            {
                settings = new AuthenticationSettings
                {
                    Name = AuthenticationSettings.GlobalName,
                    PasswordLoginEnabled = enabled,
                };
                await _dbContext.AuthenticationSettings.AddAsync(settings, cancellationToken);
            }
            else
            {
                settings.PasswordLoginEnabled = enabled;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task EnsureCanUnlinkAsync(
            Guid userId,
            Guid identityId,
            CancellationToken cancellationToken)
        {
            UserExternalIdentity identity = await _dbContext.UserExternalIdentities
                .Include(x => x.Provider)
                .SingleOrDefaultAsync(x => x.Id == identityId && x.UserId == userId, cancellationToken)
                ?? throw new AuthApiException(StatusCodes.Status404NotFound, "External identity was not found.");

            if (await IsPasswordLoginEnabledAsync(cancellationToken))
            {
                return;
            }

            bool userDisabled = await _dbContext.Users
                .Where(x => x.Id == userId)
                .Select(x => x.IsDisabled)
                .SingleOrDefaultAsync(cancellationToken);
            if (userDisabled
                || !identity.Provider.IsEnabled
                || !string.Equals(identity.Issuer, identity.Provider.Issuer, StringComparison.Ordinal))
            {
                return;
            }

            bool hasAnotherEnabledIdentity = await _dbContext.UserExternalIdentities
                .AnyAsync(
                    x => x.UserId == userId
                        && x.Id != identityId
                        && x.Provider.IsEnabled
                        && x.Issuer == x.Provider.Issuer,
                    cancellationToken);
            if (!hasAnotherEnabledIdentity)
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "This is the user's last available sign-in method while password login is disabled.");
            }
        }
    }
}
