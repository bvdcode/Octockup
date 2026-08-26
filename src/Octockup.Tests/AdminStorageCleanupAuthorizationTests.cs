// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using System.Net;

namespace Octockup.Tests
{
    public class AdminStorageCleanupAuthorizationTests
    {
        [Test]
        public async Task Controller_RejectsAuthenticatedNonAdministrator()
        {
            await using WebApplication app = await AuthorizationTestServer.CreateAsync();
            using HttpClient client = app.GetTestClient();
            client.DefaultRequestHeaders.Add(
                TestAuthenticationHandler.AuthenticatedHeader,
                bool.TrueString);

            using HttpResponseMessage response = await client.GetAsync(
                "/api/v1/admin/storage-cleanups");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }
    }
}
