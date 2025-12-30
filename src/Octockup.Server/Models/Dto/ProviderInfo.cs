// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Dto
{
    public class ProviderInfo
    {
        public string? Id { get; set; }
        public char PathSeparator { get; set; }
        public string Name { get; set; } = string.Empty;
        public IEnumerable<string> RequiredParameters { get; set; } = [];
    }
}
