using MediatR;
using AutoMapper;
using System.Security.Claims;
using Octockup.Server.Models;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using EasyExtensions.AspNetCore.Authorization.Builders;
using EasyExtensions.AspNetCore.Authorization.Services;

namespace Octockup.Server.Handlers
{

    public class CreateTokenRequestHandler(ITokenProvider _tokenProvider, AppDbContext _dbContext,
         IConfiguration _configuration, IMapper _mapper, ILogger<CreateTokenRequestHandler> _logger)
        : IRequestHandler<CreateTokenRequest, AuthResponse>
    {
        public async Task<AuthResponse> Handle(CreateTokenRequest request, CancellationToken cancellationToken)
        {
            string accessToken = _tokenProvider.CreateToken(x => GetUserClaims(x, request.User));
            int refreshLifetime = _configuration.GetValue<int>("JwtSettings:RefreshLifetimeHours");
            if (refreshLifetime <= 0)
            {
                throw new InvalidOperationException("JwtSettings:RefreshLifetimeHours must be greater than 0");
            }
            TimeSpan refreshLifetimeSpan = TimeSpan.FromHours(refreshLifetime);
            Session session = new()
            {
                User = request.User,
                UserId = request.User.Id,
                RefreshToken = _tokenProvider.CreateToken(refreshLifetimeSpan, x => GetUserClaims(x, request.User)),
            };
            _dbContext.Sessions.Add(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            UserDto userDto = _mapper.Map<UserDto>(request.User);
            _logger.LogDebug("Created token for user {User}", request.User);
            return new AuthResponse()
            {
                User = userDto,
                AccessToken = accessToken,
                RefreshToken = session.RefreshToken
            };
        }

        private static ClaimBuilder GetUserClaims(ClaimBuilder builder, User user)
        {
            ArgumentNullException.ThrowIfNull(user, nameof(user));
            ArgumentException.ThrowIfNullOrWhiteSpace(user.Username, nameof(user.Username));
            ArgumentException.ThrowIfNullOrWhiteSpace(user.Email, nameof(user.Email));

            return builder.Add(ClaimTypes.Name, user.Username)
                    .Add(ClaimTypes.Role, user.Role.ToString())
                    .Add(ClaimTypes.Email, user.Email)
                    .Add(ClaimTypes.Sid, user.Id.ToString());
        }
    }
}
