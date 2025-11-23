using Mapster;
using MediatR;
using System.Net;
using EasyExtensions.Helpers;
using Octockup.Server.Models;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;

namespace Octockup.Server.Handlers
{
    public class RefreshRequestHandler(
        ITokenProvider _tokens,
        AppDbContext _dbContext,
        ITokenProvider _tokenProvider,
        ILogger<RefreshRequestHandler> _logger)
        : IRequestHandler<RefreshRequest, AuthResponse>
    {
        public async Task<AuthResponse> Handle(RefreshRequest request, CancellationToken cancellationToken)
        {
            bool isValid = _tokenProvider.ValidateToken(request.RefreshToken);
            if (!isValid)
            {
                throw new WebApiException(HttpStatusCode.Unauthorized, nameof(RefreshToken), "Invalid refresh token");
            }
            var foundToken = _dbContext.RefreshTokens
                .Include(x => x.User)
                .FirstOrDefault(x => x.Token == request.RefreshToken && !x.RevokedAt.HasValue)
                ?? throw new WebApiException(HttpStatusCode.NotFound, nameof(RefreshToken), "Session not found");
            string accessToken = _tokens.CreateToken(x => x.Add("sub", foundToken.UserId.ToString()));
            string refreshToken = StringHelpers.CreateRandomString(64);
            foundToken.RevokedAt = DateTime.UtcNow;
            var newSession = new RefreshToken
            {
                Token = refreshToken,
                UserId = foundToken.UserId,
            };
            await _dbContext.RefreshTokens.AddAsync(newSession, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Refresh token rotated for user {UserId}", foundToken.UserId);
            return new()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = foundToken.User.Adapt<UserDto>(),
            };
        }
    }
}
