// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

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
