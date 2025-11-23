using MediatR;
using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Abstractions;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/auth")]
    public class AuthController(IMediator _mediator, IUserDataStorage _userDataStorage) : ControllerBase
    {
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
        {
            bool success = _userDataStorage.ChangePassword(request);
            if (!success)
            {
                return BadRequest("Password change failed.");
            }
            return Ok("Password changed successfully.");
        }

        [HttpPost("refresh-token")]
        public Task<AuthResponse> RefreshTokenAsync([FromBody] RefreshRequest request)
        {
            return _mediator.Send(request);
        }

        [HttpPost("login")]
        public IActionResult LoginAsync([FromBody] LoginRequest request)
        {
            var result = _mediator.Send(request);
            if (result == null)
            {
                return this.ApiUnauthorized("Invalid username or password.");
            }
            return Ok(result);
        }
    }
}
