// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Octockup.Server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        private const string EventHubPath = "/api/v1/event-hub";

        public static IServiceCollection AddOctockupJwt(this IServiceCollection services)
        {
            services.AddJwt();
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Events.OnMessageReceived = context =>
                    {
                        string? accessToken = context.Request.Query["access_token"]
                            .FirstOrDefault();
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments(EventHubPath))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    };
                });
            return services;
        }
    }
}
