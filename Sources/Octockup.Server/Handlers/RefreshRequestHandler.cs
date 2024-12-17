using MediatR;
using Octockup.Server.Models;
using Octockup.Server.Database;

namespace Octockup.Server.Handlers
{
    public class RefreshRequestHandler : IRequestHandler<RefreshRequest, AuthResponse>
    {
        public Task<AuthResponse> Handle(RefreshRequest request, CancellationToken cancellationToken)
        {
            bool isValid = _tokenProvider.ValidateToken(request.RefreshToken);
            if (!isValid)
            {
                
                return Unauthorized();
            }
            var foundToken = _dbContext.Sessions.FirstOrDefault(x => x.RefreshToken == request.RefreshToken);
            if (foundToken is null)
            {
                return NotFound();
            }
            _dbContext.Sessions.Remove(foundToken);
            await _dbContext.SaveChangesAsync();
            // JwtSettingsRefreshLifetimeHours
            int hours = 720;
            string token = _tokenProvider.CreateToken(TimeSpan.FromHours(hours));
            Session session = new()
            {
                UserId = foundToken.UserId,
                RefreshToken = _tokenProvider.CreateToken(x => x.Add(ClaimTypes.Name, "refresh"))
            };
            _dbContext.Sessions.Add(session);
            await _dbContext.SaveChangesAsync();
            return Ok(new AuthResponse(token, session.RefreshToken));
        }
    }
}
