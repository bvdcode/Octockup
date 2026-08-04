// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Services;

namespace Octockup.Server.Middleware
{
    public class AuthApiExceptionMiddleware(RequestDelegate _next, ILogger<AuthApiExceptionMiddleware> _logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (AuthApiException exception)
            {
                _logger.LogWarning(
                    "Authentication API request rejected with status {StatusCode}: {Message}",
                    exception.StatusCode,
                    exception.Message);
                context.Response.StatusCode = exception.StatusCode;
                await context.Response.WriteAsJsonAsync(new { message = exception.Message });
            }
        }
    }
}
