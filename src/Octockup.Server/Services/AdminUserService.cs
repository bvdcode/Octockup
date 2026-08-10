// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Services
{
    public class AdminUserService(
        AppDbContext _dbContext,
        IPasswordHashService? _passwords = null)
    {
        public async Task<IReadOnlyCollection<AdminUserDto>> GetUsersAsync(
            CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .OrderBy(x => x.Username)
                .Select(x => new AdminUserDto
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    Username = x.Username,
                    IsAdmin = x.IsAdmin,
                    IsDisabled = x.IsDisabled,
                    ExternalIdentityCount = x.ExternalIdentities.Count,
                })
                .ToListAsync(cancellationToken);
        }

        public Task<AdminUserDto> CreateAsync(
            string username,
            string password,
            bool isAdmin,
            CancellationToken cancellationToken)
        {
            return AuthMutationTransaction.ExecuteAsync(
                _dbContext,
                () => CreateCoreAsync(username, password, isAdmin, cancellationToken),
                cancellationToken);
        }

        private async Task<AdminUserDto> CreateCoreAsync(
            string username,
            string password,
            bool isAdmin,
            CancellationToken cancellationToken)
        {
            AuthenticationSettingsService settings = new(_dbContext);
            if (!await settings.IsPasswordLoginEnabledAsync(cancellationToken))
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "Enable password login before creating a password-based user.");
            }

            if (!UsernameValidator.TryNormalize(username, out string normalizedUsername))
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    "Username must contain 1 to 128 letters, digits, dots, dashes, or underscores.");
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new AuthApiException(StatusCodes.Status400BadRequest, "Password is required.");
            }
            if (await _dbContext.Users.AnyAsync(x => x.Username == normalizedUsername, cancellationToken))
            {
                throw new AuthApiException(StatusCodes.Status409Conflict, "Username is already in use.");
            }

            IPasswordHashService passwords = _passwords
                ?? throw new InvalidOperationException("Password hashing service is not configured.");
            User user = new()
            {
                Username = normalizedUsername,
                PasswordPhc = passwords.Hash(password),
                IsAdmin = isAdmin,
                IsDisabled = false,
            };
            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(user, 0);
        }

        public Task<AdminUserDto> UpdateAccessAsync(
            Guid actorUserId,
            Guid userId,
            bool isAdmin,
            bool isDisabled,
            CancellationToken cancellationToken)
        {
            return AuthMutationTransaction.ExecuteAsync(
                _dbContext,
                () => UpdateAccessCoreAsync(
                    actorUserId,
                    userId,
                    isAdmin,
                    isDisabled,
                    cancellationToken),
                cancellationToken);
        }

        private async Task<AdminUserDto> UpdateAccessCoreAsync(
            Guid actorUserId,
            Guid userId,
            bool isAdmin,
            bool isDisabled,
            CancellationToken cancellationToken)
        {
            await EnsureActorIsAdminAsync(actorUserId, cancellationToken);
            User user = await GetUserAsync(userId, cancellationToken);
            await EnsureUserCanBeEnabledAsync(user, isDisabled, cancellationToken);
            await EnsureEnabledAdminRemainsAsync(user, isAdmin, isDisabled, cancellationToken);

            user.IsAdmin = isAdmin;
            user.IsDisabled = isDisabled;
            if (isDisabled)
            {
                await RevokeRefreshTokensAsync(userId, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            int identityCount = await _dbContext.UserExternalIdentities
                .CountAsync(x => x.UserId == user.Id, cancellationToken);
            return ToDto(user, identityCount);
        }

        private async Task EnsureActorIsAdminAsync(
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            bool actorIsAdmin = await _dbContext.Users
                .AnyAsync(
                    x => x.Id == actorUserId && x.IsAdmin && !x.IsDisabled,
                    cancellationToken);
            if (!actorIsAdmin)
            {
                throw new AuthApiException(
                    StatusCodes.Status403Forbidden,
                    "Administrator access is required.");
            }
        }

        private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
                ?? throw new AuthApiException(StatusCodes.Status404NotFound, "User was not found.");
        }

        private async Task EnsureUserCanBeEnabledAsync(
            User user,
            bool isDisabled,
            CancellationToken cancellationToken)
        {
            if (!user.IsDisabled || isDisabled)
            {
                return;
            }

            AuthenticationSettingsService authenticationSettings = new(_dbContext);
            bool passwordLoginEnabled = await authenticationSettings.IsPasswordLoginEnabledAsync(
                cancellationToken);
            bool hasEnabledExternalIdentity = await _dbContext.UserExternalIdentities
                .AnyAsync(
                    x => x.UserId == user.Id
                        && x.Provider.IsEnabled
                        && x.Issuer == x.Provider.Issuer,
                    cancellationToken);
            if (!passwordLoginEnabled && !hasEnabledExternalIdentity)
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "Link an enabled external identity before activating this user while password login is disabled.");
            }
        }

        private async Task EnsureEnabledAdminRemainsAsync(
            User user,
            bool isAdmin,
            bool isDisabled,
            CancellationToken cancellationToken)
        {
            bool removesEnabledAdmin = user.IsAdmin
                && !user.IsDisabled
                && (!isAdmin || isDisabled);
            if (!removesEnabledAdmin)
            {
                return;
            }

            bool hasAnotherEnabledAdmin = await _dbContext.Users
                .AnyAsync(
                    x => x.Id != user.Id && x.IsAdmin && !x.IsDisabled,
                    cancellationToken);
            if (!hasAnotherEnabledAdmin)
            {
                throw new AuthApiException(
                    StatusCodes.Status409Conflict,
                    "The last active administrator cannot be disabled or demoted.");
            }
        }

        private async Task RevokeRefreshTokensAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            List<RefreshToken> refreshTokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId && x.RevokedAt == null)
                .ToListAsync(cancellationToken);
            DateTime revokedAt = DateTime.UtcNow;
            foreach (RefreshToken refreshToken in refreshTokens)
            {
                refreshToken.RevokedAt = revokedAt;
            }
        }

        private static AdminUserDto ToDto(User user, int identityCount)
        {
            return new AdminUserDto
            {
                Id = user.Id,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Username = user.Username,
                IsAdmin = user.IsAdmin,
                IsDisabled = user.IsDisabled,
                ExternalIdentityCount = identityCount,
            };
        }
    }
}
