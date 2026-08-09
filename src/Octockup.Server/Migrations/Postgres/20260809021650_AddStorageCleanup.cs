using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddStorageCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "storage_cleanup_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "text", nullable: false),
                    stored_size = table.Column<long>(type: "bigint", nullable: false),
                    original_size = table.Column<long>(type: "bigint", nullable: false),
                    compression_algorithm = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_cleanup_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_cleanup_chunks_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "storage_cleanups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    cursor_hash = table.Column<string>(type: "text", nullable: true),
                    scan_upper_bound_hash = table.Column<string>(type: "text", nullable: true),
                    scanned_chunks = table.Column<long>(type: "bigint", nullable: false),
                    total_deleted_chunks = table.Column<long>(type: "bigint", nullable: false),
                    total_reclaimed_bytes = table.Column<long>(type: "bigint", nullable: false),
                    last_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_cleanups", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_cleanups_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_storage_cleanup_chunks_module_id_hash",
                table: "storage_cleanup_chunks",
                columns: new[] { "module_id", "hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_storage_cleanups_module_id",
                table: "storage_cleanups",
                column: "module_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_cleanup_chunks");

            migrationBuilder.DropTable(
                name: "storage_cleanups");
        }
    }
}
