using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddDurableStorageCleanupJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "storage_cleanup_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_storage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_tag = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    phase = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_requested = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    snapshot_files_scanned = table.Column<long>(type: "bigint", nullable: false),
                    reference_count = table.Column<long>(type: "bigint", nullable: false),
                    referenced_chunks = table.Column<long>(type: "bigint", nullable: false),
                    storage_objects_scanned = table.Column<long>(type: "bigint", nullable: false),
                    storage_bytes_scanned = table.Column<long>(type: "bigint", nullable: false),
                    chunk_objects_scanned = table.Column<long>(type: "bigint", nullable: false),
                    referenced_objects = table.Column<long>(type: "bigint", nullable: false),
                    referenced_bytes = table.Column<long>(type: "bigint", nullable: false),
                    orphan_objects = table.Column<long>(type: "bigint", nullable: false),
                    orphan_bytes = table.Column<long>(type: "bigint", nullable: false),
                    deleted_objects = table.Column<long>(type: "bigint", nullable: false),
                    freed_bytes = table.Column<long>(type: "bigint", nullable: false),
                    missing_objects = table.Column<long>(type: "bigint", nullable: false),
                    failed_deletes = table.Column<long>(type: "bigint", nullable: false),
                    skipped_objects = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_hash_rows_deleted = table.Column<long>(type: "bigint", nullable: false),
                    current_path = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_cleanup_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_storage_cleanup_jobs_active_storage_id",
                table: "storage_cleanup_jobs",
                column: "active_storage_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_storage_cleanup_jobs_user_id_started_at",
                table: "storage_cleanup_jobs",
                columns: new[] { "user_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_cleanup_jobs");
        }
    }
}
