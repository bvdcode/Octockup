// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Abstractions;
using EasyExtensions.AspNetCore.Authorization.Builders;

namespace Octockup.Tests
{
    internal class TestTokenProvider : ITokenProvider
    {
        public bool FailCreation { get; init; }
        public TimeSpan TokenLifetime => TimeSpan.FromMinutes(15);

        public string CreateToken(Func<ClaimBuilder, ClaimBuilder>? configureClaims = null)
        {
            return CreateToken();
        }

        public string CreateToken(IClaimProvider claimProvider)
        {
            return CreateToken();
        }

        public string CreateToken(
            TimeSpan tokenLifetime,
            Func<ClaimBuilder, ClaimBuilder>? configureClaims = null)
        {
            return CreateToken();
        }

        public bool ValidateToken(string token)
        {
            return token == "access-token";
        }

        public void RotateKey()
        {
        }

        private string CreateToken()
        {
            if (FailCreation)
            {
                throw new InvalidOperationException("Simulated access token creation failure.");
            }

            return "access-token";
        }
    }
}
