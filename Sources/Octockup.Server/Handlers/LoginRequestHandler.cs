using MediatR;
using AutoMapper;
using Octockup.Server.Models;
using System.Security.Claims;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using EasyExtensions.AspNetCore.Authorization.Services;
using EasyExtensions.AspNetCore.Authorization.Builders;

namespace Octockup.Server.Handlers
{
    public class LoginRequestHandler(ILogger<LoginRequestHandler> _logger, ITokenProvider _tokenProvider,
        AppDbContext _dbContext, IConfiguration _configuration, IMapper _mapper) : IRequestHandler<LoginRequest, AuthResponse>
    {
        public async Task<AuthResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
        {
            var foundUser = _dbContext.Users.FirstOrDefault(x => x.Username.Equals(request.Username, StringComparison.CurrentCultureIgnoreCase)) 
                ?? throw new WebApiException(System.Net.HttpStatusCode.NotFound, nameof(User), "User not found");
            if (!foundUser.PasswordHash.Equals(request.PasswordHash))
            {
                _logger.LogWarning("Login attempt for '{Username}' failed", request.Username);
                throw new WebApiException(System.Net.HttpStatusCode.Unauthorized, nameof(User), "Invalid password");
            }
            _logger.LogInformation("User '{Username}' logged in", request.Username);
            string accessToken = _tokenProvider.CreateToken(x => GetUserClaims(x, foundUser));
            int refreshLifetime = _configuration.GetValue<int>("JwtSettings:RefreshLifetimeHours");
            if (refreshLifetime <= 0)
            {
                throw new InvalidOperationException("JwtSettings:RefreshLifetimeHours must be greater than 0");
            }
            TimeSpan refreshLifetimeSpan = TimeSpan.FromHours(refreshLifetime);
            Session session = new()
            {
                User = foundUser,
                UserId = foundUser.Id,
                RefreshToken = _tokenProvider.CreateToken(refreshLifetimeSpan, x => GetUserClaims(x, foundUser)),
            };
            _dbContext.Sessions.Add(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            UserDto userDto = _mapper.Map<UserDto>(foundUser);
            return new(accessToken, session.RefreshToken, userDto);
        }

        private static ClaimBuilder GetUserClaims(ClaimBuilder builder, User user)
        {
            return builder.Add(ClaimTypes.Name, user.Username)
                    .Add(ClaimTypes.Role, user.Role.ToString())
                    .Add(ClaimTypes.Email, user.Email)
                    .Add(ClaimTypes.Sid, user.Id.ToString());
        }
    }
}
