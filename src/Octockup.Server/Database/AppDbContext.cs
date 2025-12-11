// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Database
{
    public class AppDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Backup> Backups => Set<Backup>();
        public DbSet<Module> Modules => Set<Module>();
        public DbSet<Schedule> Schedules => Set<Schedule>();
        public DbSet<Snapshot> Snapshots => Set<Snapshot>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<SnapshotFile> SnapshotFiles => Set<SnapshotFile>();
        public DbSet<UploadedHash> UploadedHashes => Set<UploadedHash>();
    }
}
