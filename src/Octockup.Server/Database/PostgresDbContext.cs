// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Database
{
    /// Add-Migration Initial -Context PostgresDbContext -Output Migrations/Postgres
    public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : AppDbContext(options) { }
}
