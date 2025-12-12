using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Octockup.Server.Converters;
using System.Text.Json;

namespace Octockup.Server.Database
{
    public class SqliteDbContext(DbContextOptions<SqliteDbContext> options) : AppDbContext(options)
    {
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
                c => c.ToList()
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
