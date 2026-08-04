// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Http;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;

namespace Octockup.Tests
{
    internal class RecordingAuthSessionIssuer : IAuthSessionIssuer
    {
        public Guid? IssuedUserId { get; private set; }
        public int IssueCount { get; private set; }
        public int RotateCount { get; private set; }
        public string? RotatedToken { get; private set; }
        public TokenPairResponseDto? RotationResult { get; set; }

        public Task<TokenPairResponseDto> IssueAsync(
            User user,
            HttpResponse response,
            CancellationToken cancellationToken)
        {
            IssuedUserId = user.Id;
            IssueCount++;
            return Task.FromResult(new TokenPairResponseDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
            });
        }

        public Task<TokenPairResponseDto?> RotateAsync(
            string refreshToken,
            HttpResponse response,
            CancellationToken cancellationToken)
        {
            RotateCount++;
            RotatedToken = refreshToken;
            return Task.FromResult(RotationResult);
        }
    }
}
