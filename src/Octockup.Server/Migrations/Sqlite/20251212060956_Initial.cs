using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    token = table.Column<string>(type: "TEXT", nullable: false),
                    revoked_at = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    username = table.Column<string>(type: "TEXT", nullable: false),
                    password_phc = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "modules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tag = table.Column<string>(type: "TEXT", nullable: false),
                    destination = table.Column<int>(type: "INTEGER", nullable: false),
                    backup_module_id = table.Column<string>(type: "TEXT", nullable: false),
                    parameters = table.Column<string>(type: "TEXT", nullable: false),
                    encrypted_parameters = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modules", x => x.id);
                    table.ForeignKey(
                        name: "FK_modules_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    message = table.Column<string>(type: "TEXT", nullable: false),
                    details = table.Column<string>(type: "TEXT", nullable: true),
                    metadata = table.Column<string>(type: "TEXT", nullable: true),
                    read_at = table.Column<string>(type: "TEXT", nullable: true),
                    priority = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "backups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    storage_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tag = table.Column<string>(type: "TEXT", nullable: false),
                    ignored_paths = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backups", x => x.id);
                    table.ForeignKey(
                        name: "FK_backups_modules_source_id",
                        column: x => x.source_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_backups_modules_storage_id",
                        column: x => x.storage_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "uploaded_hashes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    module_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    hash = table.Column<string>(type: "TEXT", nullable: false),
                    stored_size = table.Column<long>(type: "INTEGER", nullable: false),
                    original_size = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploaded_hashes", x => x.id);
                    table.ForeignKey(
                        name: "FK_uploaded_hashes_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    backup_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    finished_at = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    start_at = table.Column<string>(type: "TEXT", nullable: false),
                    interval = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    error_message = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_schedules_backups_backup_id",
                        column: x => x.backup_id,
                        principalTable: "backups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    backup_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    completed_at = table.Column<string>(type: "TEXT", nullable: true),
                    total_size = table.Column<long>(type: "INTEGER", nullable: false),
                    files_count = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_snapshots_backups_backup_id",
                        column: x => x.backup_id,
                        principalTable: "backups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_snapshots_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "schedules",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "snapshot_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    size = table.Column<long>(type: "INTEGER", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    last_modified = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    hashsum = table.Column<string>(type: "TEXT", nullable: false),
                    chunk_hashes = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_snapshot_files_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backups_source_id",
                table: "backups",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_backups_storage_id",
                table: "backups",
                column: "storage_id");

            migrationBuilder.CreateIndex(
                name: "IX_backups_tag",
                table: "backups",
                column: "tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_modules_tag",
                table: "modules",
                column: "tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_modules_user_id",
                table: "modules",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedules_backup_id",
                table: "schedules",
                column: "backup_id");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_files_snapshot_id_path",
                table: "snapshot_files",
                columns: ["snapshot_id", "path"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_backup_id",
                table: "snapshots",
                column: "backup_id");

            migrationBuilder.CreateIndex(
                name: "IX_snapshots_ScheduleId",
                table: "snapshots",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_hashes_hash",
                table: "uploaded_hashes",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "IX_uploaded_hashes_module_id_hash",
                table: "uploaded_hashes",
                columns: ["module_id", "hash"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "snapshot_files");

            migrationBuilder.DropTable(
                name: "uploaded_hashes");

            migrationBuilder.DropTable(
                name: "snapshots");

            migrationBuilder.DropTable(
                name: "schedules");

            migrationBuilder.DropTable(
                name: "backups");

            migrationBuilder.DropTable(
                name: "modules");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
