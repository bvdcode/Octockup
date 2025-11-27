using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Octockup.Server.Database
{
    public class AppDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Backup> Backups => Set<Backup>();
        public DbSet<Module> Modules => Set<Module>();
        public DbSet<Schedule> Schedules => Set<Schedule>();
        public DbSet<Snapshot> Snapshots => Set<Snapshot>();
        public DbSet<SnapshotFile> SnapshotFiles => Set<SnapshotFile>();

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
    }
}
