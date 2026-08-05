// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Helpers
{
    public enum ChunkKeyVariant : byte
    {
        Legacy,
        Version2NoneEncrypted,
        Version2NonePlain,
        Version2ZstdEncrypted,
        Version2ZstdPlain,
    }
}
