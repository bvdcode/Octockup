// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            // Global DateTime converter: always store as UTC in SQLite, always read as UTC
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<UtcDateTimeConverter>();

            configurationBuilder.Properties<DateTime?>()
                .HaveConversion<UtcNullableDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- Comparers ----------

            //    Dictionary<string,string>
            var dictComparer = new ValueComparer<Dictionary<string, string>>(
                (d1, d2) =>
                    d1!.Count == d2!.Count &&
                    d1.OrderBy(kv => kv.Key).SequenceEqual(d2.OrderBy(kv => kv.Key)),
                d => d.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key, v.Value)),
                d => d.ToDictionary(e => e.Key, e => e.Value)
            );

            var stringCollectionComparer = new ValueComparer<ICollection<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v)),
                c => (ICollection<string>)c.ToList()
            );

            // ---------- Module.Parameters ----------

            modelBuilder.Entity<Module>()
                .Property(m => m.Parameters)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v)
                        ? new Dictionary<string, string>()
                        : JsonSerializer.Deserialize<Dictionary<string, string>>(v)!
                )
                .Metadata.SetValueComparer(dictComparer);
        }

        /// <summary>
        /// Ensures DateTime values are always stored and retrieved as UTC in SQLite.
        /// Throws if attempting to save non-UTC DateTime.
        /// </summary>
        private class UtcDateTimeConverter : ValueConverter<DateTime, string>
        {
            public UtcDateTimeConverter()
                : base(
                    v => ConvertToUtcString(v),
                    v => DateTime.Parse(v).ToUniversalTime())
            {
            }

            private static string ConvertToUtcString(DateTime value)
            {
                if (value.Kind != DateTimeKind.Utc)
                {
                    throw new InvalidOperationException($"Attempted to save non-UTC DateTime ({value.Kind}): {value}. All DateTime values must be UTC.");
                }
                return value.ToString("o");
            }
        }

        /// <summary>
        /// Ensures nullable DateTime values are always stored and retrieved as UTC in SQLite.
        /// Throws if attempting to save non-UTC DateTime.
        /// </summary>
        private class UtcNullableDateTimeConverter : ValueConverter<DateTime?, string?>
        {
            public UtcNullableDateTimeConverter()
                : base(
                    v => ConvertToUtcString(v),
                    v => v != null ? DateTime.Parse(v).ToUniversalTime() : null)
            {
            }

            private static string? ConvertToUtcString(DateTime? value)
            {
                if (!value.HasValue)
                {
                    return null;
                }
                if (value.Value.Kind != DateTimeKind.Utc)
                {
                    throw new InvalidOperationException($"Attempted to save non-UTC DateTime ({value.Value.Kind}): {value.Value}. All DateTime values must be UTC.");
                }
                return value.Value.ToString("o");
            }
        }
    }
}
