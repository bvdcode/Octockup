using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ReconcileUploadedHashInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "last_seen_cleanup_job_id",
                table: "uploaded_hashes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "missing_indexed_objects",
                table: "storage_cleanup_jobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_hashes_module_id_last_seen_cleanup_job_id",
                table: "uploaded_hashes",
                columns: new[] { "module_id", "last_seen_cleanup_job_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_uploaded_hashes_module_id_last_seen_cleanup_job_id",
                table: "uploaded_hashes");

            migrationBuilder.DropColumn(
                name: "last_seen_cleanup_job_id",
                table: "uploaded_hashes");

            migrationBuilder.DropColumn(
                name: "missing_indexed_objects",
                table: "storage_cleanup_jobs");
        }
    }
}
