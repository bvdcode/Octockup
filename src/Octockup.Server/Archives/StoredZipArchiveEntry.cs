// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Archives
{
    public record StoredZipArchiveEntry(
        string Name,
        long Size,
        DateTime? LastModified,
        Func<CancellationToken, Task<Stream>> OpenStreamAsync);
}
