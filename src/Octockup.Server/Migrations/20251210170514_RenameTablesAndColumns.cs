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
                name: "PK_Snapshots",
                table: "Snapshots");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Schedules",
                table: "Schedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Modules",
                table: "Modules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Backups",
                table: "Backups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UploadedHashes",
                table: "UploadedHashes");

            migrationBuilder.DropIndex(
                name: "IX_UploadedHashes_ModuleId",
                table: "UploadedHashes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SnapshotFiles",
                table: "SnapshotFiles");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "Snapshots",
                newName: "snapshots");

            migrationBuilder.RenameTable(
                name: "Schedules",
                newName: "schedules");

            migrationBuilder.RenameTable(
                name: "Modules",
                newName: "modules");

            migrationBuilder.RenameTable(
                name: "Backups",
                newName: "backups");

            migrationBuilder.RenameTable(
                name: "UploadedHashes",
                newName: "uploaded_hashes");

            migrationBuilder.RenameTable(
                name: "SnapshotFiles",
                newName: "snapshot_files");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "users",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "PasswordPhc",
                table: "users",
                newName: "password_phc");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Username",
                table: "users",
                newName: "IX_users_username");

            migrationBuilder.RenameColumn(
                name: "TotalSize",
                table: "snapshots",
                newName: "total_size");

            migrationBuilder.RenameColumn(
                name: "FilesCount",
                table: "snapshots",
                newName: "files_count");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "snapshots",
                newName: "completed_at");

            migrationBuilder.RenameColumn(
                name: "BackupId",
                table: "snapshots",
                newName: "backup_id");

            migrationBuilder.RenameIndex(
                name: "IX_Snapshots_ScheduleId",
                table: "snapshots",
                newName: "IX_snapshots_ScheduleId");

            migrationBuilder.RenameIndex(
                name: "IX_Snapshots_BackupId",
                table: "snapshots",
                newName: "IX_snapshots_backup_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "schedules",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Interval",
                table: "schedules",
                newName: "interval");

            migrationBuilder.RenameColumn(
                name: "StartAt",
                table: "schedules",
                newName: "start_at");

            migrationBuilder.RenameColumn(
                name: "FinishedAt",
                table: "schedules",
                newName: "finished_at");

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                table: "schedules",
                newName: "error_message");

            migrationBuilder.RenameColumn(
                name: "BackupId",
                table: "schedules",
                newName: "backup_id");

            migrationBuilder.RenameIndex(
                name: "IX_Schedules_BackupId",
                table: "schedules",
                newName: "IX_schedules_backup_id");

            migrationBuilder.RenameColumn(
                name: "Tag",
                table: "modules",
                newName: "tag");

            migrationBuilder.RenameColumn(
                name: "Parameters",
                table: "modules",
                newName: "parameters");

            migrationBuilder.RenameColumn(
                name: "Destination",
                table: "modules",
                newName: "destination");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "modules",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "BackupModuleId",
                table: "modules",
                newName: "backup_module_id");

            migrationBuilder.RenameIndex(
                name: "IX_Modules_Tag",
                table: "modules",
                newName: "IX_modules_tag");

            migrationBuilder.RenameIndex(
                name: "IX_Modules_UserId",
                table: "modules",
                newName: "IX_modules_user_id");

            migrationBuilder.RenameColumn(
                name: "Tag",
                table: "backups",
                newName: "tag");

            migrationBuilder.RenameColumn(
                name: "StorageId",
                table: "backups",
                newName: "storage_id");

            migrationBuilder.RenameColumn(
                name: "SourceId",
                table: "backups",
                newName: "source_id");

            migrationBuilder.RenameColumn(
                name: "IgnoredPaths",
                table: "backups",
                newName: "ignored_paths");

            migrationBuilder.RenameIndex(
                name: "IX_Backups_Tag",
                table: "backups",
                newName: "IX_backups_tag");

            migrationBuilder.RenameIndex(
                name: "IX_Backups_StorageId",
                table: "backups",
                newName: "IX_backups_storage_id");

            migrationBuilder.RenameIndex(
                name: "IX_Backups_SourceId",
                table: "backups",
                newName: "IX_backups_source_id");

            migrationBuilder.RenameColumn(
                name: "Hash",
                table: "uploaded_hashes",
                newName: "hash");

            migrationBuilder.RenameColumn(
                name: "StoredSize",
                table: "uploaded_hashes",
                newName: "stored_size");

            migrationBuilder.RenameColumn(
                name: "OriginalSize",
                table: "uploaded_hashes",
                newName: "original_size");

            migrationBuilder.RenameColumn(
                name: "ModuleId",
                table: "uploaded_hashes",
                newName: "module_id");

            migrationBuilder.RenameColumn(
                name: "Size",
                table: "snapshot_files",
                newName: "size");

            migrationBuilder.RenameColumn(
                name: "Path",
                table: "snapshot_files",
                newName: "path");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "snapshot_files",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Hashsum",
                table: "snapshot_files",
                newName: "hashsum");

            migrationBuilder.RenameColumn(
                name: "SnapshotId",
                table: "snapshot_files",
                newName: "snapshot_id");

            migrationBuilder.RenameColumn(
                name: "LastModified",
                table: "snapshot_files",
                newName: "last_modified");

            migrationBuilder.RenameColumn(
                name: "ChunkHashes",
                table: "snapshot_files",
                newName: "chunk_hashes");

            migrationBuilder.RenameIndex(
                name: "IX_SnapshotFiles_SnapshotId",
                table: "snapshot_files",
                newName: "IX_snapshot_files_snapshot_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_snapshots",
                table: "snapshots",
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

            migrationBuilder.AddPrimaryKey(
                name: "PK_uploaded_hashes",
                table: "uploaded_hashes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_snapshot_files",
                table: "snapshot_files",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_hashes_hash",
                table: "uploaded_hashes",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_hashes_module_id_hash",
                table: "uploaded_hashes",
                columns: ["module_id", "hash"],
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
                name: "PK_snapshots",
                table: "snapshots");

            migrationBuilder.DropPrimaryKey(
                name: "PK_schedules",
                table: "schedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_modules",
                table: "modules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_backups",
                table: "backups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_uploaded_hashes",
                table: "uploaded_hashes");

            migrationBuilder.DropIndex(
                name: "IX_uploaded_hashes_hash",
                table: "uploaded_hashes");

            migrationBuilder.DropIndex(
                name: "IX_uploaded_hashes_module_id_hash",
                table: "uploaded_hashes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_snapshot_files",
                table: "snapshot_files");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "snapshots",
                newName: "Snapshots");

            migrationBuilder.RenameTable(
                name: "schedules",
                newName: "Schedules");

            migrationBuilder.RenameTable(
                name: "modules",
                newName: "Modules");

            migrationBuilder.RenameTable(
                name: "backups",
                newName: "Backups");

            migrationBuilder.RenameTable(
                name: "uploaded_hashes",
                newName: "UploadedHashes");

            migrationBuilder.RenameTable(
                name: "snapshot_files",
                newName: "SnapshotFiles");

            migrationBuilder.RenameColumn(
                name: "username",
                table: "Users",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "password_phc",
                table: "Users",
                newName: "PasswordPhc");

            migrationBuilder.RenameIndex(
                name: "IX_users_username",
                table: "Users",
                newName: "IX_Users_Username");

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
                name: "IX_snapshots_ScheduleId",
                table: "Snapshots",
                newName: "IX_Snapshots_ScheduleId");

            migrationBuilder.RenameIndex(
                name: "IX_snapshots_backup_id",
                table: "Snapshots",
                newName: "IX_Snapshots_BackupId");

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
                name: "IX_schedules_backup_id",
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
                name: "IX_modules_tag",
                table: "Modules",
                newName: "IX_Modules_Tag");

            migrationBuilder.RenameIndex(
                name: "IX_modules_user_id",
                table: "Modules",
                newName: "IX_Modules_UserId");

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
                name: "IX_backups_tag",
                table: "Backups",
                newName: "IX_Backups_Tag");

            migrationBuilder.RenameIndex(
                name: "IX_backups_storage_id",
                table: "Backups",
                newName: "IX_Backups_StorageId");

            migrationBuilder.RenameIndex(
                name: "IX_backups_source_id",
                table: "Backups",
                newName: "IX_Backups_SourceId");

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
                name: "IX_snapshot_files_snapshot_id",
                table: "SnapshotFiles",
                newName: "IX_SnapshotFiles_SnapshotId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Snapshots",
                table: "Snapshots",
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

            migrationBuilder.AddPrimaryKey(
                name: "PK_UploadedHashes",
                table: "UploadedHashes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SnapshotFiles",
                table: "SnapshotFiles",
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
