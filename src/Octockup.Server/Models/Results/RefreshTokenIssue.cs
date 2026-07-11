// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Results
{
    public class RefreshTokenIssue(
        Guid userId,
        string refreshToken,
        DateTime expiresAt)
    {
        public Guid UserId { get; } = userId;
        public string RefreshToken { get; } = refreshToken;
        public DateTime ExpiresAt { get; } = expiresAt;
    }
}
