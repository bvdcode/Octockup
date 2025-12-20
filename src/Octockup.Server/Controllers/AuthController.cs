// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Requests;
using System.IdentityModel.Tokens.Jwt;

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
        public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequestDto request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                if (Request.Cookies.TryGetValue("refresh_token", out var cookieToken))
                {
                    request.RefreshToken = cookieToken;
                }
            }
            var foundToken = _dbContext.RefreshTokens.FirstOrDefault(x => x.Token == request.RefreshToken && x.RevokedAt == null);
            if (foundToken == null)
            {
                return this.ApiUnauthorized("Invalid refresh token.");
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
            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions()
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
            });
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequestDto request)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null)
            {
                int userCount = await _dbContext.Users.CountAsync();
                bool multiUserAllowed = Environment.GetEnvironmentVariable("OCTOCKUP_ALLOW_MULTIUSER") == "true";
                if (!multiUserAllowed && userCount > 0)
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
            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions()
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
            });
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> LogoutAsync()
        {
            string refreshToken = Request.Cookies["refresh_token"] ?? string.Empty;
            var foundToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == refreshToken && x.RevokedAt == null);
            if (foundToken != null)
            {
                foundToken.RevokedAt = DateTime.UtcNow;
                _dbContext.RefreshTokens.Update(foundToken);
                await _dbContext.SaveChangesAsync();
            }
            Response.Cookies.Delete("refresh_token");
            return Ok("Logged out successfully.");
        }
    }
}
