// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Models.Results;
using System.Security.Cryptography;
using System.Text;

namespace Octockup.Server.Services
{
    public class RefreshSessionService(
        AppDbContext _dbContext,
        TimeProvider _timeProvider,
        IOptions<RefreshSessionOptions> _options)
    {
        private const int TokenByteLength = 32;
        private const int EncodedTokenLength = 43;

        public async Task<RefreshTokenIssue> CreateAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            (RefreshSession session, RefreshTokenIssue issue) = CreateSession(
                userId,
                Guid.NewGuid(),
                now);
            await _dbContext.RefreshSessions.AddAsync(session, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return issue;
        }

        public async Task<RefreshTokenIssue?> RotateAsync(
            string? refreshToken,
            CancellationToken cancellationToken)
        {
            if (!IsValidToken(refreshToken))
            {
                return null;
            }

            string tokenHash = GetTokenHash(refreshToken!);
            RefreshSession? currentSession = await _dbContext.RefreshSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
            if (currentSession is null)
            {
                return null;
            }

            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            if (currentSession.RevokedAt is not null)
            {
                await RevokeFamilyAsync(
                    currentSession.FamilyId,
                    now,
                    RefreshSessionRevocationReason.ReuseDetected,
                    cancellationToken);
                return null;
            }

            if (currentSession.ExpiresAt <= now)
            {
                await RevokeSessionAsync(
                    currentSession.Id,
                    now,
                    RefreshSessionRevocationReason.Expired,
                    cancellationToken);
                return null;
            }

            (RefreshSession nextSession, RefreshTokenIssue issue) = CreateSession(
                currentSession.UserId,
                currentSession.FamilyId,
                now);

            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            int rotated = await _dbContext.RefreshSessions
                .Where(x =>
                    x.Id == currentSession.Id &&
                    x.RevokedAt == null &&
                    x.ExpiresAt > now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.RevokedAt, now)
                        .SetProperty(
                            x => x.RevocationReason,
                            RefreshSessionRevocationReason.Rotated)
                        .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);

            if (rotated != 1)
            {
                await RevokeFamilyAsync(
                    currentSession.FamilyId,
                    now,
                    RefreshSessionRevocationReason.ReuseDetected,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            await _dbContext.RefreshSessions.AddAsync(nextSession, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return issue;
        }

        public async Task<bool> RevokeAsync(
            string? refreshToken,
            CancellationToken cancellationToken)
        {
            if (!IsValidToken(refreshToken))
            {
                return false;
            }

            string tokenHash = GetTokenHash(refreshToken!);
            RefreshSession? session = await _dbContext.RefreshSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
            if (session is null)
            {
                return false;
            }

            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            int revoked = await RevokeFamilyAsync(
                session.FamilyId,
                now,
                RefreshSessionRevocationReason.Logout,
                cancellationToken);
            return revoked > 0;
        }

        public async Task<int> RevokeAllForPasswordChangeAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            return await _dbContext.RefreshSessions
                .Where(x => x.UserId == userId && x.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.RevokedAt, now)
                        .SetProperty(
                            x => x.RevocationReason,
                            RefreshSessionRevocationReason.PasswordChanged)
                        .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
        }

        private (RefreshSession Session, RefreshTokenIssue Issue) CreateSession(
            Guid userId,
            Guid familyId,
            DateTime now)
        {
            string refreshToken = WebEncoders.Base64UrlEncode(
                RandomNumberGenerator.GetBytes(TokenByteLength));
            DateTime expiresAt = now.Add(_options.Value.Lifetime);
            RefreshSession session = new()
            {
                UserId = userId,
                FamilyId = familyId,
                TokenHash = GetTokenHash(refreshToken),
                ExpiresAt = expiresAt
            };
            RefreshTokenIssue issue = new(userId, refreshToken, expiresAt);
            return (session, issue);
        }

        private Task<int> RevokeFamilyAsync(
            Guid familyId,
            DateTime revokedAt,
            RefreshSessionRevocationReason reason,
            CancellationToken cancellationToken)
        {
            return _dbContext.RefreshSessions
                .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.RevokedAt, revokedAt)
                        .SetProperty(x => x.RevocationReason, reason)
                        .SetProperty(x => x.UpdatedAt, revokedAt),
                    cancellationToken);
        }

        private Task<int> RevokeSessionAsync(
            Guid sessionId,
            DateTime revokedAt,
            RefreshSessionRevocationReason reason,
            CancellationToken cancellationToken)
        {
            return _dbContext.RefreshSessions
                .Where(x => x.Id == sessionId && x.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.RevokedAt, revokedAt)
                        .SetProperty(x => x.RevocationReason, reason)
                        .SetProperty(x => x.UpdatedAt, revokedAt),
                    cancellationToken);
        }

        private static bool IsValidToken(string? refreshToken)
        {
            return refreshToken is not null && refreshToken.Length == EncodedTokenLength;
        }

        private static string GetTokenHash(string refreshToken)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
            return Convert.ToHexString(hash);
        }
    }
}
