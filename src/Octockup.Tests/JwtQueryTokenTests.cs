// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Octockup.Server.Extensions;

namespace Octockup.Tests
{
    public class JwtQueryTokenTests
    {
        [TestCase("/api/v1/modules", null)]
        [TestCase("/api/v1/snapshots/00000000-0000-0000-0000-000000000000/download", null)]
        [TestCase("/api/v1/event-hub", "query-token")]
        [TestCase("/api/v1/event-hub/negotiate", "query-token")]
        public async Task AccessTokenQuery_IsAcceptedOnlyForEventHub(
            string path,
            string? expectedToken)
        {
            Dictionary<string, string?> settings = new()
            {
                ["JwtSettings:Key"] = "12345678901234567890123456789012",
                ["JwtSettings:Issuer"] = "test-issuer",
                ["JwtSettings:Audience"] = "test-audience"
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
            ServiceCollection services = new();
            services.AddSingleton(configuration);
            services.AddLogging();
            services.AddOctockupJwt();
            await using ServiceProvider provider = services.BuildServiceProvider();
            JwtBearerOptions options = provider
                .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);
            DefaultHttpContext httpContext = new();
            httpContext.Request.Path = path;
            httpContext.Request.QueryString = new QueryString("?access_token=query-token");
            AuthenticationScheme scheme = new(
                JwtBearerDefaults.AuthenticationScheme,
                null,
                typeof(JwtBearerHandler));
            MessageReceivedContext context = new(httpContext, scheme, options);

            await options.Events.OnMessageReceived(context);

            Assert.That(context.Token, Is.EqualTo(expectedToken));
        }
    }
}
