// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Handlers.Administration
{
    public class StartStorageCleanupCommand(Guid moduleId) : IRequest<StorageCleanupDto>
    {
        public Guid ModuleId { get; } = moduleId;
    }
}
