using MediatR;
using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class AuthController(IMediator _mediator) : ControllerBase
    {
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
        {
            await _mediator.Send(request);
            return Ok();
        }

        [HttpPost("refresh-token")]
        public Task<AuthResponse> RefreshTokenAsync([FromBody] RefreshRequest request)
        {
            return _mediator.Send(request);
        }

        [HttpPost("login")]
        public Task<AuthResponse> LoginAsync([FromBody] LoginRequest request)
        {
            return _mediator.Send(request);
        }

        [Authorize]
        [HttpGet("check-token")]
        public IActionResult CheckToken()
        {
            return Ok();
        }
    }
}
