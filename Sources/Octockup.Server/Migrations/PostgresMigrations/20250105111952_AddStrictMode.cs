using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Octockup.Server.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddStrictMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "files",
                table: "backup_snapshots");

            migrationBuilder.RenameColumn(
                name: "size",
                table: "backup_snapshots",
                newName: "total_size");

            migrationBuilder.AddColumn<bool>(
                name: "strict_mode",
                table: "backup_tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "saved_files",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    backup_snapshot_id = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sha512 = table.Column<string>(type: "text", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    source_path = table.Column<string>(type: "text", nullable: false),
                    metadata_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metadata_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_saved_files_backup_snapshots_backup_snapshot_id",
                        column: x => x.backup_snapshot_id,
                        principalTable: "backup_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_files_backup_snapshot_id",
                table: "saved_files",
                column: "backup_snapshot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_files");

            migrationBuilder.DropColumn(
                name: "strict_mode",
                table: "backup_tasks");

            migrationBuilder.RenameColumn(
                name: "total_size",
                table: "backup_snapshots",
                newName: "size");

            migrationBuilder.AddColumn<string[]>(
                name: "files",
                table: "backup_snapshots",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }
    }
}
