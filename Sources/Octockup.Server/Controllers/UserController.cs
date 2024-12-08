using Octockup.Server.Models;
using EasyExtensions.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class UserController(ILogger<UserController> _logger) : ControllerBase
    {
        [HttpPost]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for {Username}", request.Username);
            string random = StringHelpers.CreateRandomString(32);
            return Ok(new LoginResponse(random, random));
        }
    }
}
