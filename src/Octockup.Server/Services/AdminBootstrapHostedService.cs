// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class AdminBootstrapHostedService(
        IServiceScopeFactory _scopeFactory,
        ILogger<AdminBootstrapHostedService> _logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await dbContext.Users.AnyAsync(x => x.IsAdmin, cancellationToken))
            {
                return;
            }

            User? firstUser = await dbContext.Users
                .Where(x => !x.IsDisabled)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (firstUser == null)
            {
                return;
            }

            firstUser.IsAdmin = true;
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Promoted existing user {UserId} to the initial Octockup administrator", firstUser.Id);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
