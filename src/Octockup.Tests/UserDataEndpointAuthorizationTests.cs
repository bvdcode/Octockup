// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using System.Net;

namespace Octockup.Tests
{
    public class UserDataEndpointAuthorizationTests
    {
        [TestCase("PATCH", "/api/v1/modules/00000000-0000-0000-0000-000000000001/rename")]
        [TestCase("DELETE", "/api/v1/modules/00000000-0000-0000-0000-000000000001")]
        [TestCase("PATCH", "/api/v1/backups/00000000-0000-0000-0000-000000000001/ignored-paths")]
        [TestCase("PATCH", "/api/v1/backups/00000000-0000-0000-0000-000000000001/rename")]
        [TestCase("DELETE", "/api/v1/backups/00000000-0000-0000-0000-000000000001")]
        [TestCase("POST", "/api/v1/backups/00000000-0000-0000-0000-000000000001/run")]
        [TestCase("PUT", "/api/v1/backups/00000000-0000-0000-0000-000000000001/schedule")]
        [TestCase("DELETE", "/api/v1/backups/00000000-0000-0000-0000-000000000001/schedule")]
        [TestCase("POST", "/api/v1/schedules/00000000-0000-0000-0000-000000000001/cancel")]
        [TestCase("GET", "/api/v1/snapshots/00000000-0000-0000-0000-000000000001/download")]
        [TestCase("GET", "/api/v1/snapshots/00000000-0000-0000-0000-000000000001/files/00000000-0000-0000-0000-000000000002/download")]
        [TestCase("GET", "/api/v1/snapshots/00000000-0000-0000-0000-000000000001/files")]
        [TestCase("DELETE", "/api/v1/snapshots/00000000-0000-0000-0000-000000000001")]
        [TestCase("GET", "/api/v1/snapshots?backupId=00000000-0000-0000-0000-000000000001")]
        public async Task Endpoint_RejectsUnauthenticatedRequest(
            string method,
            string path)
        {
            await using WebApplication app = await AuthorizationTestServer.CreateAsync();
            using HttpClient client = app.GetTestClient();
            using HttpRequestMessage request = new(new HttpMethod(method), path);

            using HttpResponseMessage response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }
    }
}
