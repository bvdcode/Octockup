// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Octockup.Server.Database;
using Octockup.Server.Models.Results;
using Octockup.Server.Models.Requests;
using Octockup.Server.Services;
using System.IdentityModel.Tokens.Jwt;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/auth")]
    public class AuthController(
        ITokenProvider _tokens,
        AppDbContext _dbContext,
        ILogger<ActionContext> _logger,
        IPasswordHashService _passwords,
        RefreshSessionService _refreshSessions) : ControllerBase
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
            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(HttpContext.RequestAborted);
            await _dbContext.SaveChangesAsync(HttpContext.RequestAborted);
            await _refreshSessions.RevokeAllForPasswordChangeAsync(
                userId,
                HttpContext.RequestAborted);
            await transaction.CommitAsync(HttpContext.RequestAborted);
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
            RefreshTokenIssue? issue = await _refreshSessions.RotateAsync(
                request.RefreshToken,
                HttpContext.RequestAborted);
            if (issue is null)
            {
                DeleteRefreshCookie();
                return this.ApiUnauthorized("Invalid refresh token.");
            }

            string accessToken = _tokens.CreateToken(
                x => x.Add(JwtRegisteredClaimNames.Sub, issue.UserId.ToString()));
            _logger.LogInformation("Refresh token rotated for user {UserId}", issue.UserId);
            AppendRefreshCookie(issue);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = issue.RefreshToken,
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

            if (needsRehash)
            {
                user.PasswordPhc = _passwords.Hash(request.Password);
                _dbContext.Users.Update(user);
            }
            await _dbContext.SaveChangesAsync();
            RefreshTokenIssue issue = await _refreshSessions.CreateAsync(
                user.Id,
                HttpContext.RequestAborted);
            string accessToken = _tokens.CreateToken(x => x.Add(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
            _logger.LogInformation("User '{user}' logged in", request.Username);
            AppendRefreshCookie(issue);
            return Ok(new TokenPairResponseDto()
            {
                AccessToken = accessToken,
                RefreshToken = issue.RefreshToken,
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> LogoutAsync()
        {
            string refreshToken = Request.Cookies["refresh_token"] ?? string.Empty;
            await _refreshSessions.RevokeAsync(refreshToken, HttpContext.RequestAborted);
            DeleteRefreshCookie();
            return Ok("Logged out successfully.");
        }

        private void AppendRefreshCookie(RefreshTokenIssue issue)
        {
            Response.Cookies.Append("refresh_token", issue.RefreshToken, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/auth",
                Expires = new DateTimeOffset(issue.ExpiresAt)
            });
        }

        private void DeleteRefreshCookie()
        {
            Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/auth"
            });
        }
    }
}
