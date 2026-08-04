// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Text.RegularExpressions;

namespace Octockup.Server.Services
{
    public static partial class UsernameValidator
    {
        public static bool TryNormalize(string? value, out string username)
        {
            username = value?.Trim() ?? string.Empty;
            return username.Length is >= 1 and <= 128
                && ValidUsername().IsMatch(username);
        }

        [GeneratedRegex("^[a-zA-Z0-9._-]+$", RegexOptions.CultureInvariant)]
        private static partial Regex ValidUsername();
    }
}
