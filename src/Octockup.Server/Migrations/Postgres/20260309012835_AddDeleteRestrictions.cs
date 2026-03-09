using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddDeleteRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_backups_modules_source_id",
                table: "backups");

            migrationBuilder.DropForeignKey(
                name: "FK_backups_modules_storage_id",
                table: "backups");

            migrationBuilder.DropForeignKey(
                name: "FK_modules_users_user_id",
                table: "modules");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_users_user_id",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_schedules_backups_backup_id",
                table: "schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshot_files_snapshots_snapshot_id",
                table: "snapshot_files");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshots_backups_backup_id",
                table: "snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_uploaded_hashes_modules_module_id",
                table: "uploaded_hashes");

            migrationBuilder.AddForeignKey(
                name: "FK_backups_modules_source_id",
                table: "backups",
                column: "source_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_backups_modules_storage_id",
                table: "backups",
                column: "storage_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_modules_users_user_id",
                table: "modules",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_users_user_id",
                table: "notifications",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_schedules_backups_backup_id",
                table: "schedules",
                column: "backup_id",
                principalTable: "backups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_snapshot_files_snapshots_snapshot_id",
                table: "snapshot_files",
                column: "snapshot_id",
                principalTable: "snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_snapshots_backups_backup_id",
                table: "snapshots",
                column: "backup_id",
                principalTable: "backups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_uploaded_hashes_modules_module_id",
                table: "uploaded_hashes",
                column: "module_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_backups_modules_source_id",
                table: "backups");

            migrationBuilder.DropForeignKey(
                name: "FK_backups_modules_storage_id",
                table: "backups");

            migrationBuilder.DropForeignKey(
                name: "FK_modules_users_user_id",
                table: "modules");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_users_user_id",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_schedules_backups_backup_id",
                table: "schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshot_files_snapshots_snapshot_id",
                table: "snapshot_files");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshots_backups_backup_id",
                table: "snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_uploaded_hashes_modules_module_id",
                table: "uploaded_hashes");

            migrationBuilder.AddForeignKey(
                name: "FK_backups_modules_source_id",
                table: "backups",
                column: "source_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_backups_modules_storage_id",
                table: "backups",
                column: "storage_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_modules_users_user_id",
                table: "modules",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_users_user_id",
                table: "notifications",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_schedules_backups_backup_id",
                table: "schedules",
                column: "backup_id",
                principalTable: "backups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_snapshot_files_snapshots_snapshot_id",
                table: "snapshot_files",
                column: "snapshot_id",
                principalTable: "snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_snapshots_backups_backup_id",
                table: "snapshots",
                column: "backup_id",
                principalTable: "backups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_uploaded_hashes_modules_module_id",
                table: "uploaded_hashes",
                column: "module_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
