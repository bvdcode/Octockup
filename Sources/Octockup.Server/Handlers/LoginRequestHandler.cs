using MediatR;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using Octockup.Server.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;

namespace Octockup.Server.Handlers
{
    public class LoginRequestHandler(
        ITokenProvider _tokens,
        IUserDataStorage _userDataStorage,
        ILogger<LoginRequestHandler> _logger) : IRequestHandler<LoginRequest, AuthResponse?>
    {
        public async Task<AuthResponse?> Handle(LoginRequest request, CancellationToken cancellationToken)
        {
            bool isValid = _userDataStorage.ValidateUserCredentials(request.Username, request.Password);
            if (!isValid)
            {
                _logger.LogWarning("Invalid login attempt for user '{user}'", request.Username);
                return null;
            }

            string refreshToken = RefreshRequestHandler.CreateRefreshToken(request.Username);
            string accessToken = _tokens.CreateToken(x => x.Add("sub", request.Username));
            _logger.LogInformation("User '{user}' logged in", request.Username);
            return new()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = new UserDto() { Username = request.Username }
            };
        }
    }
}
