// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class OidcAuthenticationServiceTests
    {
        [TestCase("//evil.example")]
        [TestCase("/\\evil.example")]
        [TestCase("/%5Cevil.example")]
        [TestCase("/%2Fevil.example")]
        [TestCase("/safe\r\nLocation: https://evil.example")]
        public void NormalizeReturnUrl_WhenValueCanEscapeTheApplication_ReturnsDefault(string returnUrl)
        {
            string normalized = OidcAuthenticationService.NormalizeReturnUrl(returnUrl, "/login");

            Assert.That(normalized, Is.EqualTo("/login"));
        }

        [Test]
        public void NormalizeReturnUrl_WhenValueIsRelative_PreservesValue()
        {
            string normalized = OidcAuthenticationService.NormalizeReturnUrl(
                "/settings?section=authentication",
                "/login");

            Assert.That(normalized, Is.EqualTo("/settings?section=authentication"));
        }
    }
}
