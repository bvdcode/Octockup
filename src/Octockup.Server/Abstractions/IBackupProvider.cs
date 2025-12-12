// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Octockup.Server.Abstractions
{
    public interface IBackupProvider
    {
        string Id { get; }
        string Name { get; }
        char PathSeparator { get; }
        IEnumerable<string> RequiredParameters { get; }
        void SetParameters(IReadOnlyDictionary<string, string> parameters);
    }
}
