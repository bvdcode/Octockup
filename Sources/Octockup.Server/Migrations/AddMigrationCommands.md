Add-Migration AddJobErrorText -o Migrations/SQLiteMigrations -Context SQLiteDbContext
Add-Migration AddLastMessage -o Migrations/PostgresMigrations -Context PostgresDbContext