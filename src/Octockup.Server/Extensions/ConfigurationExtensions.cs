// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Octockup.Server.Extensions
{
    public static class ConfigurationExtensions
    {
        public static string GetMasterKey(this IConfiguration configuration)
        {
            string? masterKey = configuration["MasterKey"];
            if (!string.IsNullOrEmpty(masterKey))
            {
                return masterKey;
            }
            masterKey = Environment.GetEnvironmentVariable("MASTER_KEY");
            if (!string.IsNullOrEmpty(masterKey))
            {
                Environment.SetEnvironmentVariable("MASTER_KEY", null);
                return masterKey;
            }
            throw new InvalidOperationException("Master key not found in configuration 'MasterKey' or environment variable 'MASTER_KEY'.");
        }
    }
}
