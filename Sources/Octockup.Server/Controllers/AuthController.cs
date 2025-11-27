// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions;
using EasyExtensions.Helpers;
using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
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
                username = user.Username,
                displayName = user.Username + "@octockup",
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
            var foundToken = _dbContext.RefreshTokens.FirstOrDefault(x => x.Token == request.RefreshToken && x.RevokedAt == null);
            if (foundToken == null)
            {
                return new AuthResponse();
            }
            string accessToken = _tokens.CreateToken(x => x.Add(JwtRegisteredClaimNames.Sub, foundToken.UserId.ToString()));
            string refreshToken = StringHelpers.CreateRandomString(64);
            var newSession = new RefreshToken()
            {
                UserId = foundToken.UserId,
                Token = refreshToken,
            };
            _logger.LogInformation("Refresh token rotated for user {UserId}", foundToken.UserId);
            _dbContext.RefreshTokens.Add(newSession);
            foundToken.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return new AuthResponse()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            };
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null)
            {
                bool multiUserAllowed = Environment.GetEnvironmentVariable("OCTOCKUP_ALLOW_MULTIUSER") == "true";
                if (!multiUserAllowed)
                {
                    return this.ApiUnauthorized("Invalid username or password.");
                }
                user = new()
                {
                    Username = request.Username,
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

            string refreshToken = StringHelpers.CreateRandomString(64);
            var session = new RefreshToken()
            {
                UserId = user.Id,
                Token = refreshToken,
            };
            _dbContext.RefreshTokens.Add(session);
            if (needsRehash)
            {
                user.PasswordPhc = _passwords.Hash(request.Password);
                _dbContext.Users.Update(user);
            }
            await _dbContext.SaveChangesAsync();
            string accessToken = _tokens.CreateToken(x => x.Add(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
            _logger.LogInformation("User '{user}' logged in", request.Username);
            return Ok(new AuthResponse()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            });
        }
    }
}
