// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Octockup.Server.Extensions;

namespace Octockup.Tests
{
    public class ConfigurationSecurityTests
    {
        [Test]
        public void GetMasterKey_WhenKeyIsTooShort_RejectsConfiguration()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MasterKey"] = new string('a', 31)
                })
                .Build();

            Assert.That(
                () => configuration.GetMasterKey(),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void SetupDatabaseAndKeys_DerivesIndependentJwtSigningKey()
        {
            string masterKey = new('m', 32);
            WebApplicationBuilder builder = WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    EnvironmentName = "Testing"
                });
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["MasterKey"] = masterKey
                });

            builder.SetupDatabaseAndKeys();
            string? jwtKey = builder.Configuration["JwtSettings:Key"];

            Assert.Multiple(() =>
            {
                Assert.That(jwtKey, Is.Not.Null);
                Assert.That(jwtKey, Has.Length.EqualTo(32));
                Assert.That(jwtKey, Is.Not.EqualTo(masterKey));
            });
        }
    }
}
