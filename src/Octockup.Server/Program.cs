// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Extensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Extensions;
using EasyExtensions.Quartz.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Extensions;
using Octockup.Server.Hubs;
using Octockup.Server.Modules;
using Octockup.Server.Services;

namespace Octockup.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.SetupDatabaseAndKeys();

            // Configure Kestrel for large requests
            builder.Services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 1_073_741_824; // 1 GB
            });

            builder.Services.AddControllers();

            // Configure form options for large file uploads
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 1_073_741_824; // 1 GB
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartHeadersLengthLimit = int.MaxValue;
            });

            builder.Services
                .AddScoped<IBackupProvider, IMAPSource>()
                .AddScoped<IBackupProvider, S3BackupStorage>()
                .AddScoped<IBackupProvider, SFTPBackupStorage>()
                .AddScoped<IBackupProvider, FileSystemBackupSource>()
                .AddScoped<BackupDeletionService>()
                .AddScoped<SnapshotDeletionService>()
                .AddScoped<ChunkReferenceCollector>()
                .AddScoped<StorageCleanupRunner>()
                .AddScoped<StorageMaintenanceService>()
                .AddSingleton(TimeProvider.System)
                .AddSingleton<IStorageOperationCoordinator, StorageOperationCoordinator>()
                .AddSingleton<StorageCleanupCancellationRegistry>()
                .AddSingleton<StorageCleanupJobStore>()
                .AddSingleton<IStorageCleanupJobScheduler, QuartzStorageCleanupJobScheduler>()
                .AddSingleton<StorageCleanupJobManager>()
                .AddSingleton<IStorageCleanupProgressPublisher, SignalRStorageCleanupProgressPublisher>()
                .AddSingleton<StorageCleanupJobExecutor>()
                .AddPbkdf2PasswordHashService()
                .AddCpuUsageService()
                .AddQuartzJobs()
                .AddHttpContextAccessor()
                .AddJwt()
                .AddSignalR();

            string[] corsOrigins = builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? [];
            builder.Services.AddDefaultCorsWithOrigins(corsOrigins);

            var app = builder.Build();
            app.UseCors().UseDefaultFiles();
            app.MapStaticAssets();
            app.UseAuthentication()
                .UseAuthorization();
            app.MapControllers();
            app.MapFallbackToFile("/index.html");
            app.MapHub<EventHub>("/api/v1/event-hub");
            app.ApplyMigrations<AppDbContext>();
            app.Run();
        }
    }
}
