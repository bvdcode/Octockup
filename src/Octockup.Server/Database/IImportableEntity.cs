// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Database
{
    internal interface IImportableEntity
    {
        void RestoreId(Guid id);
    }
}
