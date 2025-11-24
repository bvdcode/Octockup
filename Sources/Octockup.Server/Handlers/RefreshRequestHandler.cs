using MediatR;
using EasyExtensions.Helpers;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using EasyExtensions.AspNetCore.Authorization.Abstractions;

namespace Octockup.Server.Handlers
{
    public class RefreshRequestHandler(
        ITokenProvider _tokens,
        ILogger<RefreshRequestHandler> _logger)
        : IRequestHandler<RefreshRequest, AuthResponse>
    {
        private static readonly ConcurrentBag<RefreshToken> _refreshTokens = [];

        public static string CreateRefreshToken(string username)
        {
            string refreshToken = StringHelpers.CreateRandomString(64);
            var newSession = new RefreshToken(username, refreshToken);
            _refreshTokens.Add(newSession);
            return refreshToken;
        }

        public async Task<AuthResponse> Handle(RefreshRequest request, CancellationToken cancellationToken)
        {
            var foundToken = _refreshTokens.FirstOrDefault(x => x.Token == request.RefreshToken);
            if (foundToken == null)
            {
                return new();
            }
            string accessToken = _tokens.CreateToken(x => x.Add("sub", foundToken.Username));
            string refreshToken = StringHelpers.CreateRandomString(64);
            var newSession = new RefreshToken(foundToken.Username, refreshToken);
            _logger.LogInformation("Refresh token rotated for user {UserId}", foundToken.Username);
            _refreshTokens.Add(newSession);
            return new()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = new UserDto { Username = foundToken.Username }
            };
        }
    }
}
