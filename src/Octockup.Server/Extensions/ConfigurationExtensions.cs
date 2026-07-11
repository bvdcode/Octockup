// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Extensions
{
    public static class ConfigurationExtensions
    {
        private static bool _removedMasterKey = false;

        public static string GetMasterKey(this IConfiguration configuration)
        {
            string? masterKey = configuration["MasterKey"];
            if (!string.IsNullOrEmpty(masterKey))
            {
                return ValidateMasterKey(masterKey);
            }
            if (_removedMasterKey)
            {
                throw new InvalidOperationException("Master key has already been removed from environment variables.");
            }
            masterKey = Environment.GetEnvironmentVariable("OCTOCKUP_MASTER_KEY");
            if (!string.IsNullOrEmpty(masterKey))
            {
                Environment.SetEnvironmentVariable("OCTOCKUP_MASTER_KEY", "REMOVED");
                _removedMasterKey = true;
                return ValidateMasterKey(masterKey);
            }
            throw new InvalidOperationException("Master key not found in configuration 'MasterKey' or environment variable 'OCTOCKUP_MASTER_KEY'.");
        }

        private static string ValidateMasterKey(string masterKey)
        {
            if (masterKey.Length < 32)
            {
                throw new InvalidOperationException("Master key must contain at least 32 characters.");
            }

            return masterKey;
        }
    }
}
