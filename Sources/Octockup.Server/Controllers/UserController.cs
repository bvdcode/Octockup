using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("api/v1/user")]
    public class UserController(ILogger<UserController> _logger) : ControllerBase
    {
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for {Username}", request.Username);
            return Ok(new LoginResponse("token"));
        }
    }
}
