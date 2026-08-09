using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddStorageCleanupRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "last_run_id",
                table: "storage_cleanups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "storage_cleanup_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    scanned_chunks = table.Column<long>(type: "bigint", nullable: false),
                    deleted_chunks = table.Column<long>(type: "bigint", nullable: false),
                    reclaimed_bytes = table.Column<long>(type: "bigint", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_cleanup_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_cleanup_runs_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_storage_cleanups_last_run_id",
                table: "storage_cleanups",
                column: "last_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_storage_cleanup_runs_module_id_started_at",
                table: "storage_cleanup_runs",
                columns: new[] { "module_id", "started_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_storage_cleanups_storage_cleanup_runs_last_run_id",
                table: "storage_cleanups",
                column: "last_run_id",
                principalTable: "storage_cleanup_runs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_storage_cleanups_storage_cleanup_runs_last_run_id",
                table: "storage_cleanups");

            migrationBuilder.DropTable(
                name: "storage_cleanup_runs");

            migrationBuilder.DropIndex(
                name: "IX_storage_cleanups_last_run_id",
                table: "storage_cleanups");

            migrationBuilder.DropColumn(
                name: "last_run_id",
                table: "storage_cleanups");
        }
    }
}
