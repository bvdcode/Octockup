using MediatR;
using EasyExtensions;
using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Abstractions;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;
using System.Threading.Tasks;
using Octockup.Server.Models.Requests;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/auth")]
    public class AuthController(IMediator _mediator, IUserDataStorage _userDataStorage) : ControllerBase
    {
        [Authorize]
        [HttpGet("me")]
        public IActionResult MeAsync()
        {
            string username = User.GetUserName();
            return Ok(new
            {
                username,
                id = username,
                displayName = username,
            });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
        {
            string username = User.GetUserName();
            bool success = _userDataStorage.ChangePassword(username, request.OldPassword, request.NewPassword);
            if (!success)
            {
                return BadRequest("Password change failed.");
            }
            return Ok("Password changed successfully.");
        }

        [HttpPost("refresh")]
        public Task<AuthResponse> RefreshTokenAsync([FromBody] RefreshRequest request)
        {
            return _mediator.Send(request);
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
        {
            string username = request.Username.ToLower();
            // username must contain only a-z, A-Z, 0-9, ., -, _
            foreach (char c in username)
            {
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_')
                {
                    return this.ApiUnauthorized("Invalid username or password.");
                }
            }
            var result = await _mediator.Send(request);
            if (result == null)
            {
                return this.ApiUnauthorized("Invalid username or password.");
            }
            return Ok(result);
        }
    }
}
