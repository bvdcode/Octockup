// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Handlers.Administration
{
    public class SetStorageCleanupSpeedCommand(Guid moduleId, StorageCleanupSpeed speed)
        : IRequest<StorageCleanupSpeed>
    {
        public Guid ModuleId { get; } = moduleId;
        public StorageCleanupSpeed Speed { get; } = speed;
    }
}
