using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:hstore", ",,");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    password_phc = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "modules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "text", nullable: false),
                    destination = table.Column<int>(type: "integer", nullable: false),
                    backup_module_id = table.Column<string>(type: "text", nullable: false),
                    parameters = table.Column<Dictionary<string, string>>(type: "hstore", nullable: false),
                    encrypted_parameters = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "backups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "text", nullable: false),
                    ignored_paths = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "text", nullable: false),
                    stored_size = table.Column<long>(type: "bigint", nullable: false),
                    original_size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    backup_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    interval = table.Column<TimeSpan>(type: "interval", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    backup_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_size = table.Column<long>(type: "bigint", nullable: false),
                    files_count = table.Column<int>(type: "integer", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    path = table.Column<string>(type: "text", nullable: false),
                    hashsum = table.Column<string>(type: "text", nullable: false),
                    chunk_hashes = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
