// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Models.Results;
using System.Security.Cryptography;
using System.Text;

namespace Octockup.Server.Services
{
    public class DownloadTicketService(
        AppDbContext _dbContext,
        TimeProvider _timeProvider,
        IOptions<DownloadTicketOptions> _options)
    {
        private const int TokenByteLength = 32;
        private const int EncodedTokenLength = 43;

        public async Task<DownloadTicketDto?> CreateSnapshotArchiveAsync(
            Guid userId,
            Guid snapshotId,
            CancellationToken cancellationToken)
        {
            bool snapshotExists = await _dbContext.Snapshots
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == snapshotId &&
                        x.CompletedAt != null &&
                        x.Backup.Source.UserId == userId,
                    cancellationToken);

            return snapshotExists
                ? await CreateAsync(
                    userId,
                    DownloadTicketKind.SnapshotArchive,
                    snapshotId,
                    null,
                    false,
                    cancellationToken)
                : null;
        }

        public async Task<DownloadTicketDto?> CreateSnapshotFileAsync(
            Guid userId,
            Guid snapshotId,
            Guid fileId,
            CancellationToken cancellationToken)
        {
            bool snapshotFileExists = await _dbContext.SnapshotFiles
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == fileId &&
                        x.SnapshotId == snapshotId &&
                        x.Snapshot.Backup.Source.UserId == userId,
                    cancellationToken);

            return snapshotFileExists
                ? await CreateAsync(
                    userId,
                    DownloadTicketKind.SnapshotFile,
                    snapshotId,
                    fileId,
                    false,
                    cancellationToken)
                : null;
        }

        public Task<DownloadTicketDto> CreateServerBackupAsync(
            Guid userId,
            bool includeFiles,
            CancellationToken cancellationToken)
        {
            return CreateAsync(
                userId,
                DownloadTicketKind.ServerBackup,
                null,
                null,
                includeFiles,
                cancellationToken);
        }

        public Task<DownloadTicketGrant?> ConsumeSnapshotArchiveAsync(
            string? token,
            Guid snapshotId,
            CancellationToken cancellationToken)
        {
            return ConsumeAsync(
                token,
                DownloadTicketKind.SnapshotArchive,
                snapshotId,
                null,
                cancellationToken);
        }

        public Task<DownloadTicketGrant?> ConsumeSnapshotFileAsync(
            string? token,
            Guid snapshotId,
            Guid fileId,
            CancellationToken cancellationToken)
        {
            return ConsumeAsync(
                token,
                DownloadTicketKind.SnapshotFile,
                snapshotId,
                fileId,
                cancellationToken);
        }

        public Task<DownloadTicketGrant?> ConsumeServerBackupAsync(
            string? token,
            CancellationToken cancellationToken)
        {
            return ConsumeAsync(
                token,
                DownloadTicketKind.ServerBackup,
                null,
                null,
                cancellationToken);
        }

        private async Task<DownloadTicketDto> CreateAsync(
            Guid userId,
            DownloadTicketKind kind,
            Guid? resourceId,
            Guid? secondaryResourceId,
            bool includeFiles,
            CancellationToken cancellationToken)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            DateTime expiresAt = now.Add(_options.Value.Lifetime);
            string token = WebEncoders.Base64UrlEncode(
                RandomNumberGenerator.GetBytes(TokenByteLength));
            DownloadTicket ticket = new()
            {
                UserId = userId,
                TokenHash = GetTokenHash(token),
                Kind = kind,
                ResourceId = resourceId,
                SecondaryResourceId = secondaryResourceId,
                IncludeFiles = includeFiles,
                ExpiresAt = expiresAt
            };

            await _dbContext.DownloadTickets.AddAsync(ticket, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new DownloadTicketDto
            {
                Ticket = token,
                ExpiresAt = expiresAt
            };
        }

        private async Task<DownloadTicketGrant?> ConsumeAsync(
            string? token,
            DownloadTicketKind kind,
            Guid? resourceId,
            Guid? secondaryResourceId,
            CancellationToken cancellationToken)
        {
            if (token is null || token.Length != EncodedTokenLength)
            {
                return null;
            }

            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            string tokenHash = GetTokenHash(token);
            DownloadTicket? ticket = await _dbContext.DownloadTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TokenHash == tokenHash &&
                        x.Kind == kind &&
                        x.ResourceId == resourceId &&
                        x.SecondaryResourceId == secondaryResourceId &&
                        x.ConsumedAt == null &&
                        x.ExpiresAt > now,
                    cancellationToken);
            if (ticket is null)
            {
                return null;
            }

            int consumed = await _dbContext.DownloadTickets
                .Where(x =>
                    x.Id == ticket.Id &&
                    x.ConsumedAt == null &&
                    x.ExpiresAt > now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.ConsumedAt, now)
                        .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);

            return consumed == 1
                ? new DownloadTicketGrant(ticket.UserId, ticket.IncludeFiles)
                : null;
        }

        private static string GetTokenHash(string token)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash);
        }
    }
}
