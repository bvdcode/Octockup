using Octockup.Server.Models;
using EasyExtensions.Helpers;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using EasyExtensions.AspNetCore.Authorization.Services;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class AuthController(ILogger<AuthController> _logger, ITokenProvider _tokenProvider) : ControllerBase
    {
        [HttpPost(nameof(Login))]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for {Username}", request.Username);
            string token = _tokenProvider.CreateToken(x => x.Add(ClaimTypes.Name, request.Username));
            Session session = new()
            {
                UserId = 1,
                RefreshToken = StringHelpers.CreateRandomString(32)
            };
            return Ok(new LoginResponse(token, session.RefreshToken));
        }

        [Authorize]
        [HttpGet(nameof(Check))]
        public IActionResult Check()
        {
            return Ok();
        }
    }
}
