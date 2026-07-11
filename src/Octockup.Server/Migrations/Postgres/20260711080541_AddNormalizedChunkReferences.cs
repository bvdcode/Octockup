using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddNormalizedChunkReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "chunk_references_indexed",
                table: "snapshot_files",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "snapshot_chunk_references",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    chunk_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_chunk_references", x => x.id);
                    table.ForeignKey(
                        name: "FK_snapshot_chunk_references_snapshot_files_snapshot_file_id",
                        column: x => x.snapshot_file_id,
                        principalTable: "snapshot_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_snapshot_chunk_references_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_chunk_references_snapshot_file_id_ordinal",
                table: "snapshot_chunk_references",
                columns: new[] { "snapshot_file_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_chunk_references_snapshot_id",
                table: "snapshot_chunk_references",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_chunk_references_storage_id_chunk_hash",
                table: "snapshot_chunk_references",
                columns: new[] { "storage_id", "chunk_hash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "snapshot_chunk_references");

            migrationBuilder.DropColumn(
                name: "chunk_references_indexed",
                table: "snapshot_files");
        }
    }
}
