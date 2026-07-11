// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class ServerBackupJsonParserState
    {
        public ServerBackupSection CurrentSection { get; set; } =
            ServerBackupSection.None;
        public string? PendingProperty { get; set; }
        public bool RootStarted { get; set; }
        public bool RootCompleted { get; set; }
        public int NextSectionIndex { get; set; }
    }
}
