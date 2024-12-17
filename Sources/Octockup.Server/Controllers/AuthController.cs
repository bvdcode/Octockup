using EasyExtensions;
using Octockup.Server.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Authorization.Services;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class AuthController(ILogger<AuthController> _logger, ITokenProvider _tokenProvider,
        AppDbContext _dbContext) : ControllerBase
    {
        [HttpPost(nameof(Refresh))]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            _logger.LogInformation("Refresh attempt for {RefreshToken}", request.RefreshToken);
            int userId = User.GetId();
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
                UserId = 1,
                RefreshToken = _tokenProvider.CreateToken(x => x.Add(ClaimTypes.Name, "refresh"))
            };
            return Ok(new TokenResponse(token, session.RefreshToken));
        }

        [HttpPost(nameof(Login))]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for {Username}", request.Username);
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
            return Ok(new TokenResponse(token, session.RefreshToken));
        }

        [Authorize]
        [HttpGet(nameof(Check))]
        public IActionResult Check()
        {
            return Ok();
        }
    }
}
