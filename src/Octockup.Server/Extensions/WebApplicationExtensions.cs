// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Services;

namespace Octockup.Server.Extensions
{
    public static class WebApplicationExtensions
    {
        public static async Task InitializeBackupOwnershipAsync(
            this WebApplication application,
            CancellationToken cancellationToken = default)
        {
            await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
            BackupOwnershipInitializer initializer = scope.ServiceProvider
                .GetRequiredService<BackupOwnershipInitializer>();
            await initializer.InitializeAsync(cancellationToken);
        }
    }
}
