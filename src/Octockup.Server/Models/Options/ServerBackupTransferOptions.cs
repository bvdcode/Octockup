// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Options
{
    public class ServerBackupTransferOptions
    {
        public long MaximumImportBytes { get; set; } = 16L * 1024 * 1024 * 1024;

        public string ImportDirectory { get; set; } = Path.Combine(
            Path.GetTempPath(),
            "octockup-imports");
    }
}
