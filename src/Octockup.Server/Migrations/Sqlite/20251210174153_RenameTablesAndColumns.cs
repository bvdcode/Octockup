using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations
{
    /// <inheritdoc />
    public partial class RenameTablesAndColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Backups_Modules_SourceId",
                table: "Backups");

            migrationBuilder.DropForeignKey(
                name: "FK_Backups_Modules_StorageId",
                table: "Backups");

            migrationBuilder.DropForeignKey(
                name: "FK_Modules_Users_UserId",
                table: "Modules");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Backups_BackupId",
                table: "Schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_SnapshotFiles_Snapshots_SnapshotId",
                table: "SnapshotFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Snapshots_Backups_BackupId",
                table: "Snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_Snapshots_Schedules_ScheduleId",
                table: "Snapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_UploadedHashes_Modules_ModuleId",
                table: "UploadedHashes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UploadedHashes",
                table: "UploadedHashes");

            migrationBuilder.DropIndex(
                name: "IX_UploadedHashes_ModuleId",
                table: "UploadedHashes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Snapshots",
                table: "Snapshots");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SnapshotFiles",
                table: "SnapshotFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Schedules",
                table: "Schedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Modules",
                table: "Modules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Backups",
                table: "Backups");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users1");

            migrationBuilder.RenameTable(
                name: "UploadedHashes",
                newName: "uploaded_hashes1");

            migrationBuilder.RenameTable(
                name: "Snapshots",
                newName: "snapshots1");

            migrationBuilder.RenameTable(
                name: "SnapshotFiles",
                newName: "snapshot_files1");

            migrationBuilder.RenameTable(
                name: "Schedules",
                newName: "schedules1");

            migrationBuilder.RenameTable(
                name: "Modules",
                newName: "modules1");

            migrationBuilder.RenameTable(
                name: "Backups",
                newName: "backups1");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "users1",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "PasswordPhc",
                table: "users1",
                newName: "password_phc");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Username",
                table: "users1",
                newName: "IX_users1_username");

            migrationBuilder.RenameColumn(
                name: "Hash",
                table: "uploaded_hashes1",
                newName: "hash");

            migrationBuilder.RenameColumn(
                name: "StoredSize",
                table: "uploaded_hashes1",
                newName: "stored_size");

            migrationBuilder.RenameColumn(
                name: "OriginalSize",
                table: "uploaded_hashes1",
                newName: "original_size");

            migrationBuilder.RenameColumn(
                name: "ModuleId",
                table: "uploaded_hashes1",
                newName: "module_id");

            migrationBuilder.RenameColumn(
                name: "TotalSize",
                table: "snapshots1",
                newName: "total_size");

            migrationBuilder.RenameColumn(
                name: "FilesCount",
                table: "snapshots1",
                newName: "files_count");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "snapshots1",
                newName: "completed_at");

            migrationBuilder.RenameColumn(
                name: "BackupId",
                table: "snapshots1",
                newName: "backup_id");

            migrationBuilder.RenameIndex(
                name: "IX_Snapshots_ScheduleId",
                table: "snapshots1",
                newName: "IX_snapshots1_ScheduleId");

            migrationBuilder.RenameIndex(
                name: "IX_Snapshots_BackupId",
                table: "snapshots1",
                newName: "IX_snapshots1_backup_id");

            migrationBuilder.RenameColumn(
                name: "Size",
                table: "snapshot_files1",
                newName: "size");

            migrationBuilder.RenameColumn(
                name: "Path",
                table: "snapshot_files1",
                newName: "path");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "snapshot_files1",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Hashsum",
                table: "snapshot_files1",
                newName: "hashsum");

            migrationBuilder.RenameColumn(
                name: "SnapshotId",
                table: "snapshot_files1",
                newName: "snapshot_id");

            migrationBuilder.RenameColumn(
                name: "LastModified",
                table: "snapshot_files1",
                newName: "last_modified");

            migrationBuilder.RenameColumn(
                name: "ChunkHashes",
                table: "snapshot_files1",
                newName: "chunk_hashes");

            migrationBuilder.RenameIndex(
                name: "IX_SnapshotFiles_SnapshotId",
                table: "snapshot_files1",
                newName: "IX_snapshot_files1_snapshot_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "schedules1",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Interval",
                table: "schedules1",
                newName: "interval");

            migrationBuilder.RenameColumn(
                name: "StartAt",
                table: "schedules1",
                newName: "start_at");

            migrationBuilder.RenameColumn(
                name: "FinishedAt",
                table: "schedules1",
                newName: "finished_at");

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                table: "schedules1",
                newName: "error_message");

            migrationBuilder.RenameColumn(
                name: "BackupId",
                table: "schedules1",
                newName: "backup_id");

            migrationBuilder.RenameIndex(
                name: "IX_Schedules_BackupId",
                table: "schedules1",
                newName: "IX_schedules1_backup_id");

            migrationBuilder.RenameColumn(
                name: "Tag",
                table: "modules1",
                newName: "tag");

            migrationBuilder.RenameColumn(
                name: "Parameters",
                table: "modules1",
                newName: "parameters");

            migrationBuilder.RenameColumn(
                name: "Destination",
                table: "modules1",
                newName: "destination");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "modules1",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "BackupModuleId",
                table: "modules1",
                newName: "backup_module_id");

            migrationBuilder.RenameIndex(
                name: "IX_Modules_UserId",
                table: "modules1",
                newName: "IX_modules1_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_Modules_Tag",
                table: "modules1",
                newName: "IX_modules1_tag");

            migrationBuilder.RenameColumn(
                name: "Tag",
                table: "backups1",
                newName: "tag");

            migrationBuilder.RenameColumn(
                name: "StorageId",
                table: "backups1",
                newName: "storage_id");

            migrationBuilder.RenameColumn(
                name: "SourceId",
                table: "backups1",
                newName: "source_id");

            migrationBuilder.RenameColumn(
                name: "IgnoredPaths",
                table: "backups1",
                newName: "ignored_paths");

            migrationBuilder.RenameIndex(
                name: "IX_Backups_Tag",
                table: "backups1",
                newName: "IX_backups1_tag");

            migrationBuilder.RenameIndex(
                name: "IX_Backups_StorageId",
                table: "backups1",
                newName: "IX_backups1_storage_id");

            migrationBuilder.RenameIndex(
                name: "IX_Backups_SourceId",
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
                name: "IX_uploaded_hashes1_hash",
                table: "uploaded_hashes1",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_hashes1_module_id_hash",
                table: "uploaded_hashes1",
                columns: ["module_id", "hash"],
                unique: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropIndex(
                name: "IX_uploaded_hashes1_hash",
                table: "uploaded_hashes1");

            migrationBuilder.DropIndex(
                name: "IX_uploaded_hashes1_module_id_hash",
                table: "uploaded_hashes1");

            migrationBuilder.DropPrimaryKey(
                name: "PK_snapshots1",
                table: "snapshots1");

            migrationBuilder.DropPrimaryKey(
                name: "PK_snapshot_files1",
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
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "uploaded_hashes1",
                newName: "UploadedHashes");

            migrationBuilder.RenameTable(
                name: "snapshots1",
                newName: "Snapshots");

            migrationBuilder.RenameTable(
                name: "snapshot_files1",
                newName: "SnapshotFiles");

            migrationBuilder.RenameTable(
                name: "schedules1",
                newName: "Schedules");

            migrationBuilder.RenameTable(
                name: "modules1",
                newName: "Modules");

            migrationBuilder.RenameTable(
                name: "backups1",
                newName: "Backups");

            migrationBuilder.RenameColumn(
                name: "username",
                table: "Users",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "password_phc",
                table: "Users",
                newName: "PasswordPhc");

            migrationBuilder.RenameIndex(
                name: "IX_users1_username",
                table: "Users",
                newName: "IX_Users_Username");

            migrationBuilder.RenameColumn(
                name: "hash",
                table: "UploadedHashes",
                newName: "Hash");

            migrationBuilder.RenameColumn(
                name: "stored_size",
                table: "UploadedHashes",
                newName: "StoredSize");

            migrationBuilder.RenameColumn(
                name: "original_size",
                table: "UploadedHashes",
                newName: "OriginalSize");

            migrationBuilder.RenameColumn(
                name: "module_id",
                table: "UploadedHashes",
                newName: "ModuleId");

            migrationBuilder.RenameColumn(
                name: "total_size",
                table: "Snapshots",
                newName: "TotalSize");

            migrationBuilder.RenameColumn(
                name: "files_count",
                table: "Snapshots",
                newName: "FilesCount");

            migrationBuilder.RenameColumn(
                name: "completed_at",
                table: "Snapshots",
                newName: "CompletedAt");

            migrationBuilder.RenameColumn(
                name: "backup_id",
                table: "Snapshots",
                newName: "BackupId");

            migrationBuilder.RenameIndex(
                name: "IX_snapshots1_ScheduleId",
                table: "Snapshots",
                newName: "IX_Snapshots_ScheduleId");

            migrationBuilder.RenameIndex(
                name: "IX_snapshots1_backup_id",
                table: "Snapshots",
                newName: "IX_Snapshots_BackupId");

            migrationBuilder.RenameColumn(
                name: "size",
                table: "SnapshotFiles",
                newName: "Size");

            migrationBuilder.RenameColumn(
                name: "path",
                table: "SnapshotFiles",
                newName: "Path");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "SnapshotFiles",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "hashsum",
                table: "SnapshotFiles",
                newName: "Hashsum");

            migrationBuilder.RenameColumn(
                name: "snapshot_id",
                table: "SnapshotFiles",
                newName: "SnapshotId");

            migrationBuilder.RenameColumn(
                name: "last_modified",
                table: "SnapshotFiles",
                newName: "LastModified");

            migrationBuilder.RenameColumn(
                name: "chunk_hashes",
                table: "SnapshotFiles",
                newName: "ChunkHashes");

            migrationBuilder.RenameIndex(
                name: "IX_snapshot_files1_snapshot_id",
                table: "SnapshotFiles",
                newName: "IX_SnapshotFiles_SnapshotId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Schedules",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "interval",
                table: "Schedules",
                newName: "Interval");

            migrationBuilder.RenameColumn(
                name: "start_at",
                table: "Schedules",
                newName: "StartAt");

            migrationBuilder.RenameColumn(
                name: "finished_at",
                table: "Schedules",
                newName: "FinishedAt");

            migrationBuilder.RenameColumn(
                name: "error_message",
                table: "Schedules",
                newName: "ErrorMessage");

            migrationBuilder.RenameColumn(
                name: "backup_id",
                table: "Schedules",
                newName: "BackupId");

            migrationBuilder.RenameIndex(
                name: "IX_schedules1_backup_id",
                table: "Schedules",
                newName: "IX_Schedules_BackupId");

            migrationBuilder.RenameColumn(
                name: "tag",
                table: "Modules",
                newName: "Tag");

            migrationBuilder.RenameColumn(
                name: "parameters",
                table: "Modules",
                newName: "Parameters");

            migrationBuilder.RenameColumn(
                name: "destination",
                table: "Modules",
                newName: "Destination");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "Modules",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "backup_module_id",
                table: "Modules",
                newName: "BackupModuleId");

            migrationBuilder.RenameIndex(
                name: "IX_modules1_user_id",
                table: "Modules",
                newName: "IX_Modules_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_modules1_tag",
                table: "Modules",
                newName: "IX_Modules_Tag");

            migrationBuilder.RenameColumn(
                name: "tag",
                table: "Backups",
                newName: "Tag");

            migrationBuilder.RenameColumn(
                name: "storage_id",
                table: "Backups",
                newName: "StorageId");

            migrationBuilder.RenameColumn(
                name: "source_id",
                table: "Backups",
                newName: "SourceId");

            migrationBuilder.RenameColumn(
                name: "ignored_paths",
                table: "Backups",
                newName: "IgnoredPaths");

            migrationBuilder.RenameIndex(
                name: "IX_backups1_tag",
                table: "Backups",
                newName: "IX_Backups_Tag");

            migrationBuilder.RenameIndex(
                name: "IX_backups1_storage_id",
                table: "Backups",
                newName: "IX_Backups_StorageId");

            migrationBuilder.RenameIndex(
                name: "IX_backups1_source_id",
                table: "Backups",
                newName: "IX_Backups_SourceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UploadedHashes",
                table: "UploadedHashes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Snapshots",
                table: "Snapshots",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SnapshotFiles",
                table: "SnapshotFiles",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Schedules",
                table: "Schedules",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Modules",
                table: "Modules",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Backups",
                table: "Backups",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_UploadedHashes_ModuleId",
                table: "UploadedHashes",
                column: "ModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Backups_Modules_SourceId",
                table: "Backups",
                column: "SourceId",
                principalTable: "Modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Backups_Modules_StorageId",
                table: "Backups",
                column: "StorageId",
                principalTable: "Modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Modules_Users_UserId",
                table: "Modules",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Backups_BackupId",
                table: "Schedules",
                column: "BackupId",
                principalTable: "Backups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SnapshotFiles_Snapshots_SnapshotId",
                table: "SnapshotFiles",
                column: "SnapshotId",
                principalTable: "Snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Snapshots_Backups_BackupId",
                table: "Snapshots",
                column: "BackupId",
                principalTable: "Backups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Snapshots_Schedules_ScheduleId",
                table: "Snapshots",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_UploadedHashes_Modules_ModuleId",
                table: "UploadedHashes",
                column: "ModuleId",
                principalTable: "Modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
