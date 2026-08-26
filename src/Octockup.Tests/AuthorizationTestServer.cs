// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Octockup.Server.Extensions;

namespace Octockup.Tests
{
    internal static class AuthorizationTestServer
    {
        public static async Task<WebApplication> CreateAsync()
        {
            WebApplicationOptions options = new()
            {
                ApplicationName = "Octockup.Server",
            };
            WebApplicationBuilder builder = WebApplication.CreateBuilder(options);
            builder.WebHost.UseTestServer();
            builder.Services
                .AddAuthentication(TestAuthenticationHandler.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    AuthenticationExtensions.AdminPolicy,
                    policy => policy.RequireClaim("is_admin", bool.TrueString));
            });
            builder.Services.AddControllers();

            WebApplication app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            return app;
        }
    }
}
