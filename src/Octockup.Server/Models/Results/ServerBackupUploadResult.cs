// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Results
{
    public readonly record struct ServerBackupUploadResult(
        ServerBackupUploadStatus Status,
        long BytesWritten);
}
