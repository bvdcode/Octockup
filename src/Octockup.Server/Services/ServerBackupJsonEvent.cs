// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;
using System.Text.Json;

namespace Octockup.Server.Services
{
    public class ServerBackupJsonEvent(
        ServerBackupSection section,
        JsonDocument? document,
        bool sectionCompleted) : IDisposable
    {
        public ServerBackupSection Section { get; } = section;
        public JsonDocument? Document { get; } = document;
        public bool SectionCompleted { get; } = sectionCompleted;

        public void Dispose()
        {
            Document?.Dispose();
        }
    }
}
