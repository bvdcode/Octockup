// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Database
{
    public abstract class AppDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Backup> Backups => Set<Backup>();
        public DbSet<Module> Modules => Set<Module>();
        public DbSet<Schedule> Schedules => Set<Schedule>();
        public DbSet<Snapshot> Snapshots => Set<Snapshot>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<SnapshotFile> SnapshotFiles => Set<SnapshotFile>();
        public DbSet<UploadedHash> UploadedHashes => Set<UploadedHash>();
        public DbSet<StorageCleanupJob> StorageCleanupJobs => Set<StorageCleanupJob>();
        public DbSet<DownloadTicket> DownloadTickets => Set<DownloadTicket>();
    }
}
