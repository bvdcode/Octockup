using MediatR;
using System.Net;
using Octockup.Server.Models;
using Octockup.Server.Database;
using EasyExtensions.EntityFrameworkCore.Exceptions;

namespace Octockup.Server.Handlers
{
    public class LoginRequestHandler(ILogger<LoginRequestHandler> _logger, AppDbContext _dbContext,
        IMediator _mediator) : IRequestHandler<LoginRequest, AuthResponse>
    {
        public async Task<AuthResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
        {
            var foundUser = _dbContext.Users.FirstOrDefault(x => x.Username.Equals(request.Username))
                ?? throw new WebApiException(HttpStatusCode.NotFound, nameof(User), "User not found");
            if (!foundUser.PasswordHash.Equals(request.PasswordHash))
            {
                _logger.LogWarning("Login attempt for '{user}' failed", foundUser);
                throw new WebApiException(HttpStatusCode.Unauthorized, nameof(User), "Invalid password");
            }
            _logger.LogInformation("User '{user}' logged in", foundUser);
            CreateTokenRequest createTokenRequest = new(foundUser);
            return await _mediator.Send(createTokenRequest, cancellationToken);
        }
    }
}
