using Mapster;
using MediatR;
using System.Net;
using Octockup.Server.Models;
using EasyExtensions.Helpers;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using EasyExtensions.Abstractions;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;

namespace Octockup.Server.Handlers
{
    public class LoginRequestHandler(
        ITokenProvider _tokens,
        AppDbContext _dbContext,
        IPasswordHashService _passwords,
        ILogger<LoginRequestHandler> _logger) : IRequestHandler<LoginRequest, AuthResponse>
    {
        public async Task<AuthResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
        {
            var foundUser = _dbContext.Users.FirstOrDefault(x => x.Username.Equals(request.Username))
                ?? throw new WebApiException(HttpStatusCode.NotFound, nameof(User), "User not found");
            bool isValid = _passwords.Verify(request.Password, foundUser.PasswordPhc, out bool needsUpgrade);
            if (!isValid)
            {
                _logger.LogWarning("Login attempt for '{user}' failed", foundUser);
                throw new WebApiException(HttpStatusCode.Unauthorized, nameof(User), "Invalid password");
            }
            RefreshToken refreshToken = new()
            {
                UserId = foundUser.Id,
                Token = StringHelpers.CreateRandomString(64)
            };
            await _dbContext.AddAsync(refreshToken, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            string accessToken = _tokens.CreateToken(x => x.Add("sub", foundUser.Id.ToString()));
            _logger.LogInformation("User '{user}' logged in", foundUser);
            return new()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                User = foundUser.Adapt<UserDto>(),
            };
        }
    }
}
