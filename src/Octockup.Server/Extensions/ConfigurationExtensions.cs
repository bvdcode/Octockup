// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

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
                return masterKey;
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
                return masterKey;
            }
            throw new InvalidOperationException("Master key not found in configuration 'MasterKey' or environment variable 'OCTOCKUP_MASTER_KEY'.");
        }
    }
}
