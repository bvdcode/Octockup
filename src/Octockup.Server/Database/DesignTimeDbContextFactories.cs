// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Octockup.Server.Database
{
    public class SqliteDbContextFactory : IDesignTimeDbContextFactory<SqliteDbContext>
    {
        public SqliteDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<SqliteDbContext>()
                .UseSqlite("Data Source=octockup.design.sqlite")
                .Options;

            return new SqliteDbContext(options);
        }
    }

    public class PostgresDbContextFactory : IDesignTimeDbContextFactory<PostgresDbContext>
    {
        public PostgresDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql("Host=localhost;Database=octockup_design;Username=postgres;Password=postgres")
                .Options;

            return new PostgresDbContext(options);
        }
    }
}
