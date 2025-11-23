using MediatR;
using System.Net;
using Octockup.Server.Models;
using Octockup.Server.Database;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using EasyExtensions.Abstractions;

namespace Octockup.Server.Handlers
{
    public class LoginRequestHandler(
        IMediator _mediator,
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
            _logger.LogInformation("User '{user}' logged in", foundUser);
            return new()
            {
                User = foundUser.Adapt<UserDto>(),
            };
        }
    }
}
