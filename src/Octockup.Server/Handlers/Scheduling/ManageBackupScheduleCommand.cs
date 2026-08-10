// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Handlers.Scheduling
{
    public class ManageBackupScheduleCommand(
        Guid userId,
        Guid backupId,
        BackupScheduleAction action,
        int? intervalMinutes = null) : IRequest<Guid?>
    {
        public Guid UserId { get; } = userId;
        public Guid BackupId { get; } = backupId;
        public BackupScheduleAction Action { get; } = action;
        public int? IntervalMinutes { get; } = intervalMinutes;
    }
}
