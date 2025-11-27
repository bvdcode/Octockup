// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov


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
