// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Authorization;
using Octockup.Server.Controllers;
using Octockup.Server.Extensions;
using System.Reflection;

namespace Octockup.Tests
{
    public class AdminStorageCleanupAuthorizationTests
    {
        [Test]
        public void Controller_RequiresAdministratorPolicy()
        {
            AuthorizeAttribute? authorize = typeof(AdminStorageCleanupController)
                .GetCustomAttribute<AuthorizeAttribute>();

            Assert.That(authorize, Is.Not.Null);
            Assert.That(authorize!.Policy, Is.EqualTo(AuthenticationExtensions.AdminPolicy));
        }
    }
}
