using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddSnapshotArchiveJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "snapshot_archive_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    phase = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_requested = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    total_files = table.Column<long>(type: "bigint", nullable: false),
                    processed_files = table.Column<long>(type: "bigint", nullable: false),
                    total_bytes = table.Column<long>(type: "bigint", nullable: false),
                    processed_bytes = table.Column<long>(type: "bigint", nullable: false),
                    prepared_chunk_references = table.Column<long>(type: "bigint", nullable: false),
                    current_path = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_archive_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_archive_jobs_active_snapshot_id",
                table: "snapshot_archive_jobs",
                column: "active_snapshot_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_archive_jobs_user_id_started_at",
                table: "snapshot_archive_jobs",
                columns: new[] { "user_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "snapshot_archive_jobs");
        }
    }
}
