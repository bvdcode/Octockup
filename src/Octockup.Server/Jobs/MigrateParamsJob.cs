// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Quartz;

namespace Octockup.Server.Jobs
{
    [JobTrigger(days: 30, repeatForever: false, startNow: true)]
    public class MigrateParamsJob(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        ILogger<MigrateParamsJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            List<Module> modules = await _dbContext.Modules.ToListAsync();
            foreach (Module? module in modules)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                if (module.Parameters.Count == 0)
                {
                    continue;
                }

                foreach (KeyValuePair<string, string> item in module.Parameters)
                {
                    module.Params(_crypto)[item.Key] = item.Value;
                    _logger.LogInformation("Migrated parameter '{ParamKey}' for Module {ModuleId}.", item.Key, module.Id);
                }
                module.Parameters.Clear();
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Finished migrating parameters for Module {ModuleId}.", module.Id);
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }
    }
}
