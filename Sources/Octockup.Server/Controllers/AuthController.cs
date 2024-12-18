using MediatR;
using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/[controller]")]
    public class AuthController(IMediator _mediator) : ControllerBase
    {
        [HttpPost(nameof(Password))]
        public async Task<IActionResult> Password([FromBody] ChangePasswordRequest request)
        {
            await _mediator.Send(request);
            return Ok();
        }

        [HttpPost(nameof(Refresh))]
        public async Task<AuthResponse> Refresh([FromBody] RefreshRequest request)
        {
            return await _mediator.Send(request);
        }

        [HttpPost(nameof(Login))]
        public async Task<AuthResponse> Login([FromBody] LoginRequest request)
        {
            return await _mediator.Send(request);
        }

        [Authorize]
        [HttpGet(nameof(Check))]
        public IActionResult Check()
        {
            return Ok();
        }
    }
}
