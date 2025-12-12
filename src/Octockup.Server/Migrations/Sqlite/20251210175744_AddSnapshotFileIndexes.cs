using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotFileIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_backups1_modules1_source_id",
                table: "backups1");

            migrationBuilder.DropForeignKey(
                name: "FK_backups1_modules1_storage_id",
                table: "backups1");

            migrationBuilder.DropForeignKey(
                name: "FK_modules1_users1_user_id",
                table: "modules1");

            migrationBuilder.DropForeignKey(
                name: "FK_schedules1_backups1_backup_id",
                table: "schedules1");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshot_files1_snapshots1_snapshot_id",
                table: "snapshot_files1");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshots1_backups1_backup_id",
                table: "snapshots1");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshots1_schedules1_ScheduleId",
                table: "snapshots1");

            migrationBuilder.DropForeignKey(
                name: "FK_uploaded_hashes1_modules1_module_id",
                table: "uploaded_hashes1");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users1",
                table: "users1");

            migrationBuilder.DropPrimaryKey(
                name: "PK_uploaded_hashes1",
                table: "uploaded_hashes1");

            migrationBuilder.DropPrimaryKey(
                name: "PK_snapshots1",
                table: "snapshots1");

            migrationBuilder.DropPrimaryKey(
                name: "PK_snapshot_files1",
                table: "snapshot_files1");

            migrationBuilder.DropIndex(
                name: "IX_snapshot_files1_snapshot_id",
                table: "snapshot_files1");

            migrationBuilder.DropPrimaryKey(
                name: "PK_schedules1",
                table: "schedules1");

            migrationBuilder.DropPrimaryKey(
                name: "PK_modules1",
                table: "modules1");

            migrationBuilder.DropPrimaryKey(
                name: "PK_backups1",
                table: "backups1");

            migrationBuilder.RenameTable(
                name: "users1",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "uploaded_hashes1",
                newName: "uploaded_hashes");

            migrationBuilder.RenameTable(
                name: "snapshots1",
                newName: "snapshots");

            migrationBuilder.RenameTable(
                name: "snapshot_files1",
                newName: "snapshot_files");

            migrationBuilder.RenameTable(
                name: "schedules1",
                newName: "schedules");

            migrationBuilder.RenameTable(
                name: "modules1",
                newName: "modules");

            migrationBuilder.RenameTable(
                name: "backups1",
                newName: "backups");

            migrationBuilder.RenameIndex(
                name: "IX_users1_username",
                table: "users",
                newName: "IX_users_username");

            migrationBuilder.RenameIndex(
                name: "IX_uploaded_hashes1_module_id_hash",
                table: "uploaded_hashes",
                newName: "IX_uploaded_hashes_module_id_hash");

            migrationBuilder.RenameIndex(
                name: "IX_uploaded_hashes1_hash",
                table: "uploaded_hashes",
                newName: "IX_uploaded_hashes_hash");

            migrationBuilder.RenameIndex(
                name: "IX_snapshots1_ScheduleId",
                table: "snapshots",
                newName: "IX_snapshots_ScheduleId");

            migrationBuilder.RenameIndex(
                name: "IX_snapshots1_backup_id",
                table: "snapshots",
                newName: "IX_snapshots_backup_id");

            migrationBuilder.RenameIndex(
                name: "IX_schedules1_backup_id",
                table: "schedules",
                newName: "IX_schedules_backup_id");

            migrationBuilder.RenameIndex(
                name: "IX_modules1_user_id",
                table: "modules",
                newName: "IX_modules_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_modules1_tag",
                table: "modules",
                newName: "IX_modules_tag");

            migrationBuilder.RenameIndex(
                name: "IX_backups1_tag",
                table: "backups",
                newName: "IX_backups_tag");

            migrationBuilder.RenameIndex(
                name: "IX_backups1_storage_id",
                table: "backups",
                newName: "IX_backups_storage_id");

            migrationBuilder.RenameIndex(
                name: "IX_backups1_source_id",
                table: "backups",
                newName: "IX_backups_source_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_uploaded_hashes",
                table: "uploaded_hashes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_snapshots",
                table: "snapshots",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_snapshot_files",
                table: "snapshot_files",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_schedules",
                table: "schedules",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_modules",
                table: "modules",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_backups",
                table: "backups",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_files_snapshot_id_path",
                table: "snapshot_files",
                columns: ["snapshot_id", "path"],
                unique: true);

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
                name: "FK_snapshots_schedules_ScheduleId",
                table: "snapshots",
                column: "ScheduleId",
                principalTable: "schedules",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_uploaded_hashes_modules_module_id",
                table: "uploaded_hashes",
                column: "module_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
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
                name: "FK_schedules_backups_backup_id",
                table: "schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshot_files_snapshots_snapshot_id",
                table: "snapshot_files");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshots_backups_backup_id",
                table: "snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_snapshots_schedules_ScheduleId",
                table: "snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_uploaded_hashes_modules_module_id",
                table: "uploaded_hashes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_uploaded_hashes",
                table: "uploaded_hashes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_snapshots",
                table: "snapshots");

            migrationBuilder.DropPrimaryKey(
                name: "PK_snapshot_files",
                table: "snapshot_files");

            migrationBuilder.DropIndex(
                name: "IX_snapshot_files_snapshot_id_path",
                table: "snapshot_files");

            migrationBuilder.DropPrimaryKey(
                name: "PK_schedules",
                table: "schedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_modules",
                table: "modules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_backups",
                table: "backups");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "users1");

            migrationBuilder.RenameTable(
                name: "uploaded_hashes",
                newName: "uploaded_hashes1");

            migrationBuilder.RenameTable(
                name: "snapshots",
                newName: "snapshots1");

            migrationBuilder.RenameTable(
                name: "snapshot_files",
                newName: "snapshot_files1");

            migrationBuilder.RenameTable(
                name: "schedules",
                newName: "schedules1");

            migrationBuilder.RenameTable(
                name: "modules",
                newName: "modules1");

            migrationBuilder.RenameTable(
                name: "backups",
                newName: "backups1");

            migrationBuilder.RenameIndex(
                name: "IX_users_username",
                table: "users1",
                newName: "IX_users1_username");

            migrationBuilder.RenameIndex(
                name: "IX_uploaded_hashes_module_id_hash",
                table: "uploaded_hashes1",
                newName: "IX_uploaded_hashes1_module_id_hash");

            migrationBuilder.RenameIndex(
                name: "IX_uploaded_hashes_hash",
                table: "uploaded_hashes1",
                newName: "IX_uploaded_hashes1_hash");

            migrationBuilder.RenameIndex(
                name: "IX_snapshots_ScheduleId",
                table: "snapshots1",
                newName: "IX_snapshots1_ScheduleId");

            migrationBuilder.RenameIndex(
                name: "IX_snapshots_backup_id",
                table: "snapshots1",
                newName: "IX_snapshots1_backup_id");

            migrationBuilder.RenameIndex(
                name: "IX_schedules_backup_id",
                table: "schedules1",
                newName: "IX_schedules1_backup_id");

            migrationBuilder.RenameIndex(
                name: "IX_modules_user_id",
                table: "modules1",
                newName: "IX_modules1_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_modules_tag",
                table: "modules1",
                newName: "IX_modules1_tag");

            migrationBuilder.RenameIndex(
                name: "IX_backups_tag",
                table: "backups1",
                newName: "IX_backups1_tag");

            migrationBuilder.RenameIndex(
                name: "IX_backups_storage_id",
                table: "backups1",
                newName: "IX_backups1_storage_id");

            migrationBuilder.RenameIndex(
                name: "IX_backups_source_id",
                table: "backups1",
                newName: "IX_backups1_source_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users1",
                table: "users1",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_uploaded_hashes1",
                table: "uploaded_hashes1",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_snapshots1",
                table: "snapshots1",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_snapshot_files1",
                table: "snapshot_files1",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_schedules1",
                table: "schedules1",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_modules1",
                table: "modules1",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_backups1",
                table: "backups1",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_files1_snapshot_id",
                table: "snapshot_files1",
                column: "snapshot_id");

            migrationBuilder.AddForeignKey(
                name: "FK_backups1_modules1_source_id",
                table: "backups1",
                column: "source_id",
                principalTable: "modules1",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_backups1_modules1_storage_id",
                table: "backups1",
                column: "storage_id",
                principalTable: "modules1",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_modules1_users1_user_id",
                table: "modules1",
                column: "user_id",
                principalTable: "users1",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_schedules1_backups1_backup_id",
                table: "schedules1",
                column: "backup_id",
                principalTable: "backups1",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_snapshot_files1_snapshots1_snapshot_id",
                table: "snapshot_files1",
                column: "snapshot_id",
                principalTable: "snapshots1",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_snapshots1_backups1_backup_id",
                table: "snapshots1",
                column: "backup_id",
                principalTable: "backups1",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_snapshots1_schedules1_ScheduleId",
                table: "snapshots1",
                column: "ScheduleId",
                principalTable: "schedules1",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_uploaded_hashes1_modules1_module_id",
                table: "uploaded_hashes1",
                column: "module_id",
                principalTable: "modules1",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
