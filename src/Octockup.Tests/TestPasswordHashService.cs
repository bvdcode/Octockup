// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;

namespace Octockup.Tests
{
    internal class TestPasswordHashService : IPasswordHashService
    {
        public int PasswordHashVersion => 1;

        public string Hash(string password)
        {
            return "hash:" + password;
        }

        public bool Verify(string password, string passwordPhc)
        {
            return passwordPhc == Hash(password);
        }

        public bool Verify(string password, string passwordPhc, out bool needsRehash)
        {
            needsRehash = false;
            return Verify(password, passwordPhc);
        }
    }
}
