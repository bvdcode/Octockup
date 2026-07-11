// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;

namespace Octockup.Server.Models.Requests
{
    public class SnapshotArchiveJobQueryRequest
    {
        [MaxLength(200)]
        public IReadOnlyList<Guid> SnapshotIds { get; set; } = [];
    }
}
