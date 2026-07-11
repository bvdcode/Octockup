// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Text.Json;

namespace Octockup.Server.Services
{
    public readonly record struct ServerBackupParseResult(
        long ConsumedBytes,
        JsonReaderState ReaderState,
        bool NeedsMoreData);
}
