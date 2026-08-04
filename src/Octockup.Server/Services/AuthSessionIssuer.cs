// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Helpers;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using System.IdentityModel.Tokens.Jwt;

namespace Octockup.Server.Services
{
    public class AuthSessionIssuer(
        ITokenProvider _tokens,
        AppDbContext _dbContext,
        ILogger<AuthSessionIssuer> _logger) : IAuthSessionIssuer
    {
        public const string SessionMarker = "cookie-session";

        public async Task<TokenPairResponseDto> IssueAsync(
            User user,
            HttpResponse response,
            CancellationToken cancellationToken)
        {
            (string RefreshToken, string AccessToken) session = await AuthMutationTransaction.ExecuteAsync(
                _dbContext,
                async () =>
                {
                    bool userIsActive = await _dbContext.Users.AnyAsync(
                        x => x.Id == user.Id && !x.IsDisabled,
                        cancellationToken);
                    if (!userIsActive)
                    {
                        throw new AuthApiException(
                            StatusCodes.Status401Unauthorized,
                            "User account is disabled.");
                    }

                    string refreshToken = StringHelpers.CreateRandomString(64);
                    await _dbContext.RefreshTokens.AddAsync(
                        new RefreshToken
                        {
                            UserId = user.Id,
                            Token = refreshToken,
                        },
                        cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    string accessToken = _tokens.CreateToken(
                        claims => claims.Add(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
                    return (refreshToken, accessToken);
                },
                cancellationToken);

            response.Cookies.Append(
                "refresh_token",
                session.RefreshToken,
                CreateRefreshCookieOptions());
            _logger.LogInformation("Authentication session issued for user {UserId}", user.Id);
            return new TokenPairResponseDto
            {
                AccessToken = session.AccessToken,
                RefreshToken = SessionMarker,
            };
        }

        public async Task<TokenPairResponseDto?> RotateAsync(
            string refreshToken,
            HttpResponse response,
            CancellationToken cancellationToken)
        {
            (Guid UserId, string RefreshToken, string AccessToken)? rotation = await AuthMutationTransaction
                .ExecuteAsync<(Guid UserId, string RefreshToken, string AccessToken)?>(
                _dbContext,
                async () =>
                {
                    RefreshToken? currentSession = await _dbContext.RefreshTokens
                        .SingleOrDefaultAsync(
                            x => x.Token == refreshToken && x.RevokedAt == null,
                            cancellationToken);
                    if (currentSession is null)
                    {
                        return null;
                    }

                    User? user = await _dbContext.Users
                        .SingleOrDefaultAsync(x => x.Id == currentSession.UserId, cancellationToken);
                    currentSession.RevokedAt = DateTime.UtcNow;
                    if (user is null || user.IsDisabled)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        return null;
                    }

                    string nextRefreshToken = StringHelpers.CreateRandomString(64);
                    await _dbContext.RefreshTokens.AddAsync(
                        new RefreshToken
                        {
                            UserId = user.Id,
                            Token = nextRefreshToken,
                        },
                        cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    string accessToken = _tokens.CreateToken(
                        claims => claims.Add(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
                    return (user.Id, nextRefreshToken, accessToken);
                },
                cancellationToken);
            if (rotation is null)
            {
                return null;
            }

            response.Cookies.Append(
                "refresh_token",
                rotation.Value.RefreshToken,
                CreateRefreshCookieOptions());
            _logger.LogInformation("Refresh token rotated for user {UserId}", rotation.Value.UserId);
            return new TokenPairResponseDto
            {
                AccessToken = rotation.Value.AccessToken,
                RefreshToken = SessionMarker,
            };
        }

        public static CookieOptions CreateRefreshCookieOptions()
        {
            return new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/auth",
                Expires = DateTimeOffset.UtcNow.AddDays(30),
            };
        }
    }
}
