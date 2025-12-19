// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

namespace Octockup.Server.Models.Requests
{
    public record RenameModuleRequest
    {
        public required string NewTag { get; init; }
    }
}
