// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models
{
    public readonly record struct StorageChunkObject(
        string ChunkKey,
        string Path,
        long Size);
}
