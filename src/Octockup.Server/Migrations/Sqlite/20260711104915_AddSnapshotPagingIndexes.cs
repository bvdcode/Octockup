using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddSnapshotPagingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_snapshots_backup_id",
                table: "snapshots");

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_backup_id_completed_at_id",
                table: "snapshots",
                columns: new[] { "backup_id", "completed_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_archive_jobs_user_id_snapshot_id_started_at",
                table: "snapshot_archive_jobs",
                columns: new[] { "user_id", "snapshot_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_snapshots_backup_id_completed_at_id",
                table: "snapshots");

            migrationBuilder.DropIndex(
                name: "IX_snapshot_archive_jobs_user_id_snapshot_id_started_at",
                table: "snapshot_archive_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_backup_id",
                table: "snapshots",
                column: "backup_id");
        }
    }
}
