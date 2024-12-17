using EasyExtensions.AspNetCore.Authorization.Services;
using MediatR;
using Octockup.Server.Database;
using Octockup.Server.Models;

namespace Octockup.Server.Handlers
{
    public class LoginRequestHandler(ILogger<LoginRequestHandler> _logger, ITokenProvider _tokenProvider) : IRequestHandler<LoginRequest, AuthResponse>
    {
        public Task<AuthResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Login attempt for '{Username}'", request.Username);
            string token = _tokenProvider.CreateToken(x =>
            {
                return x.Add(ClaimTypes.Name, request.Username)
                        .Add(ClaimTypes.Sid, "1");
            });
            Session session = new()
            {
                UserId = 1,
                RefreshToken = _tokenProvider.CreateToken(x => x.Add(ClaimTypes.Name, "refresh"))
            };
            _dbContext.Sessions.Add(session);
            await _dbContext.SaveChangesAsync();
            return Ok(new AuthResponse(token, session.RefreshToken));
        }
    }
}
