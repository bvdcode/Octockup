using EasyExtensions;
using EasyExtensions.Helpers;
using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using Octockup.Server.Models.Requests;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/auth")]
    public class AuthController(
        ITokenProvider _tokens,
        AppDbContext _dbContext,
        ILogger<ActionContext> _logger,
        IPasswordHashService _passwords) : ControllerBase
    {
        private static readonly ConcurrentBag<RefreshToken> _refreshTokens = [];
        public record RefreshToken(Guid UserId, string Token);

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> MeAsync()
        {
            Guid userId = User.GetUserId();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            return Ok(new
            {
                id = user.Id,
                username = user.UsernameRename,
                displayName = user.UsernameRename,
            });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
        {
            Guid userId = User.GetUserId();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            bool isValid = _passwords.Verify(request.OldPassword, user.PasswordPhc);
            if (!isValid)
            {
                return BadRequest("Old password is incorrect.");
            }
            user.PasswordPhc = _passwords.Hash(request.NewPassword);
            _dbContext.Users.Update(user);
            var result = await _dbContext.SaveChangesAsync();
            bool success = result > 0;
            if (!success)
            {
                return BadRequest("Password change failed.");
            }
            return Ok("Password changed successfully.");
        }

        [HttpPost("refresh")]
        public async Task<AuthResponse> RefreshTokenAsync([FromBody] RefreshRequest request)
        {
            var foundToken = _refreshTokens.FirstOrDefault(x => x.Token == request.RefreshToken);
            if (foundToken == null)
            {
                return new AuthResponse();
            }
            string accessToken = _tokens.CreateToken(x => x.Add(JwtRegisteredClaimNames.Sub, foundToken.UserId.ToString()));
            string refreshToken = StringHelpers.CreateRandomString(64);
            var newSession = new RefreshToken(foundToken.UserId, refreshToken);
            _logger.LogInformation("Refresh token rotated for user {UserId}", foundToken.UserId);
            _refreshTokens.Add(newSession);
            return new AuthResponse()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            };

        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
        {
            var user = await _dbContext.Users.FirstAsync(u => u.UsernameRename == request.Username);
            if (user == null)
            {
                user = new()
                {
                    UsernameRename = request.Username,
                    PasswordPhc = _passwords.Hash(request.Password)
                };
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
            }
            bool isValid = _passwords.Verify(request.Password, user.PasswordPhc, out bool needsRehash);
            if (!isValid)
            {
                _logger.LogWarning("Invalid login attempt for user '{user}'", request.Username);
                return this.ApiUnauthorized("Invalid username or password.");
            }

            string refreshToken = CreateRefreshToken(user.Id);
            string accessToken = _tokens.CreateToken(x => x.Add(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
            _logger.LogInformation("User '{user}' logged in", request.Username);
            return Ok(new AuthResponse()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            });
        }

        public static string CreateRefreshToken(Guid userId)
        {
            string refreshToken = StringHelpers.CreateRandomString(64);
            var newSession = new RefreshToken(userId, refreshToken);
            _refreshTokens.Add(newSession);
            return refreshToken;
        }
    }
}
