// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Octockup.Server.Abstractions;
using Octockup.Server.Authorization;
using Octockup.Server.Services;

namespace Octockup.Server.Extensions
{
    public static class AuthenticationExtensions
    {
        public const string AdminPolicy = "Admin";

        public static IServiceCollection AddOctockupAuthentication(this IServiceCollection services)
        {
            services.AddMediator();
            services.AddScoped<AuthenticationSettingsService>();
            services.AddScoped<AdminUserService>();
            services.AddScoped<OidcProviderService>();
            services.AddScoped<OidcAuthenticationService>();
            services.AddScoped<IAuthSessionIssuer, AuthSessionIssuer>();
            services.AddScoped<IAuthorizationHandler, ActiveUserAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();
            services.AddHostedService<AdminBootstrapHostedService>();
            services.AddHttpClient<OidcDiscoveryService>();
            services.AddAuthorization(options =>
            {
                ActiveUserRequirement activeUser = new();
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(activeUser)
                    .Build();
                options.AddPolicy(
                    AdminPolicy,
                    new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .AddRequirements(activeUser, new AdminRequirement())
                        .Build());
            });
            return services;
        }
    }
}
