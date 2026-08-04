// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Octockup.Server.Database;

namespace Octockup.Server.Abstractions
{
    public interface IAuthSessionIssuer
    {
        Task<TokenPairResponseDto> IssueAsync(
            User user,
            HttpResponse response,
            CancellationToken cancellationToken);

        Task<TokenPairResponseDto?> RotateAsync(
            string refreshToken,
            HttpResponse response,
            CancellationToken cancellationToken);
    }
}
