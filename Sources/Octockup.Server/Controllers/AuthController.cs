using Octockup.Server.Models;
using EasyExtensions.Helpers;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Authorization.Services;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class AuthController(ILogger<AuthController> _logger, ITokenProvider _tokenProvider) : ControllerBase
    {
        [HttpPost(nameof(Refresh))]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            await Task.Delay(1000);
            _logger.LogInformation("Refresh attempt for {RefreshToken}", request.RefreshToken);
            string token = _tokenProvider.CreateToken(x => x.Add(ClaimTypes.Name, "refresh"));
            Session session = new()
            {
                UserId = 1,
                RefreshToken = StringHelpers.CreateRandomString(32)
            };
            return Ok(new TokenResponse(token, session.RefreshToken));
        }

        [HttpPost(nameof(Login))]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            await Task.Delay(1000);
            _logger.LogInformation("Login attempt for {Username}", request.Username);
            string token = _tokenProvider.CreateToken(x => x.Add(ClaimTypes.Name, request.Username));
            Session session = new()
            {
                UserId = 1,
                RefreshToken = StringHelpers.CreateRandomString(32)
            };
            return Ok(new TokenResponse(token, session.RefreshToken));
        }

        [Authorize]
        [HttpGet(nameof(Check))]
        public async Task<IActionResult> Check()
        {
            await Task.Delay(1000);
            _logger.LogInformation("User {Username} checked in.", User.Identity.Name);
            return Ok();
        }
    }
}
