// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Results
{
    public class DownloadTicketGrant(Guid userId, bool includeFiles)
    {
        public Guid UserId { get; } = userId;
        public bool IncludeFiles { get; } = includeFiles;
    }
}
