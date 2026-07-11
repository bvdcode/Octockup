// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class SnapshotArchiveRunContext(
        SnapshotArchiveJob job,
        Guid runId,
        string fileName)
    {
        public SnapshotArchiveJob Job { get; } = job;
        public Guid RunId { get; } = runId;
        public string FileName { get; } = fileName;
    }
}
