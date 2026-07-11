// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Extensions;
using EasyExtensions.Quartz.Extensions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Extensions;
using Octockup.Server.Hubs;
using Octockup.Server.Models.Options;
using Octockup.Server.Modules;
using Octockup.Server.Services;

namespace Octockup.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.SetupDatabaseAndKeys();

            builder.Services.AddControllers();
            builder.Services
                .AddOptions<DownloadTicketOptions>()
                .BindConfiguration("DownloadTickets")
                .Validate(
                    options => options.Lifetime > TimeSpan.Zero &&
                        options.Lifetime <= TimeSpan.FromMinutes(15),
                    "Download ticket lifetime must be between zero and 15 minutes.")
                .ValidateOnStart();
            builder.Services
                .AddOptions<RefreshSessionOptions>()
                .BindConfiguration("RefreshSessions")
                .Validate(
                    options => options.Lifetime > TimeSpan.Zero &&
                        options.Lifetime <= TimeSpan.FromDays(90),
                    "Refresh session lifetime must be between zero and 90 days.")
                .ValidateOnStart();
            builder.Services
                .AddOptions<BackupExecutionOptions>()
                .BindConfiguration("BackupExecution")
                .Validate(
                    options => options.MaxConcurrentBackups > 0,
                    "Maximum concurrent backups must be positive.")
                .Validate(
                    options => options.MaxChunkLookupMemoryBytes >= 64 * 1024,
                    "Chunk lookup memory must be at least 64 KiB.")
                .ValidateOnStart();
            builder.Services
                .AddOptions<BackupProgressOptions>()
                .BindConfiguration("BackupProgress")
                .Validate(
                    options => options.PublishInterval >= TimeSpan.FromMilliseconds(100) &&
                        options.PublishInterval <= TimeSpan.FromSeconds(10),
                    "Backup progress publish interval must be between 100 milliseconds and 10 seconds.")
                .Validate(
                    options => options.AggregateLogInterval >= options.PublishInterval,
                    "Backup aggregate log interval must not be shorter than the publish interval.")
                .Validate(
                    options => options.TransportTimeout >= TimeSpan.FromSeconds(1) &&
                        options.TransportTimeout <= TimeSpan.FromMinutes(2),
                    "Progress transport timeout must be between one second and two minutes.")
                .ValidateOnStart();
            builder.Services
                .AddOptions<ServerBackupTransferOptions>()
                .BindConfiguration("ServerBackupTransfer")
                .Validate(
                    options => options.MaximumImportBytes > 0,
                    "Maximum server backup import size must be positive.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.ImportDirectory),
                    "Server backup import directory must be configured.")
                .ValidateOnStart();

            builder.Services
                .AddScoped<IBackupProvider, IMAPSource>()
                .AddScoped<IBackupProvider, S3BackupStorage>()
                .AddScoped<IBackupProvider, SFTPBackupStorage>()
                .AddScoped<IBackupProvider, FileSystemBackupSource>()
                .AddScoped<BackupDeletionService>()
                .AddScoped<BackupListService>()
                .AddScoped<SnapshotDeletionService>()
                .AddScoped<SnapshotPageService>()
                .AddScoped<SnapshotFilePageService>()
                .AddScoped<SnapshotArchiveJobService>()
                .AddScoped<SnapshotArchiveRunner>()
                .AddScoped<SnapshotArchiveExecutionService>()
                .AddScoped<DownloadTicketService>()
                .AddScoped<ServerBackupExportService>()
                .AddScoped<ServerBackupImportService>()
                .AddScoped<ServerBackupUploadService>()
                .AddScoped<ServerBackupJsonStreamReader>()
                .AddScoped<RefreshSessionService>()
                .AddScoped<BackupOwnershipInitializer>()
                .AddScoped<ScheduleNextRunInitializer>()
                .AddScoped<UploadedChunkLookup>()
                .AddScoped<PreviousSnapshotFileLookup>()
                .AddScoped<UploadedHashWriter>()
                .AddScoped<SnapshotChunkReferenceWriter>()
                .AddScoped<SnapshotChunkReferenceIndexer>()
                .AddScoped<StorageCleanupRunner>()
                .AddScoped<StorageMaintenanceService>()
                .AddSingleton(TimeProvider.System)
                .AddSingleton<IStorageOperationCoordinator, StorageOperationCoordinator>()
                .AddSingleton<StorageCleanupCancellationRegistry>()
                .AddSingleton<SnapshotArchiveCancellationRegistry>()
                .AddSingleton<StorageCleanupJobStore>()
                .AddSingleton<IStorageCleanupJobScheduler, QuartzStorageCleanupJobScheduler>()
                .AddSingleton<StorageCleanupJobManager>()
                .AddSingleton<IStorageCleanupProgressTransport, SignalRStorageCleanupProgressTransport>()
                .AddSingleton<IStorageCleanupProgressPublisher, CoalescingStorageCleanupProgressPublisher>()
                .AddSingleton<IScheduleProgressPublisher, SignalRScheduleProgressPublisher>()
                .AddSingleton<ISnapshotArchiveProgressTransport, SignalRSnapshotArchiveProgressTransport>()
                .AddSingleton<ISnapshotArchiveProgressPublisher, CoalescingSnapshotArchiveProgressPublisher>()
                .AddSingleton<StorageCleanupJobExecutor>()
                .AddPbkdf2PasswordHashService()
                .AddCpuUsageService()
                .AddQuartzJobs()
                .AddHttpContextAccessor()
                .AddOctockupJwt()
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
            await app.InitializeBackupOwnershipAsync();
            await app.RunAsync();
        }
    }
}
