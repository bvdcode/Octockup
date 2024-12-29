using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Octockup.Server.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddLastMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "last_error",
                table: "backup_tasks",
                newName: "last_message");

            migrationBuilder.CreateTable(
                name: "backup_snapshots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    files = table.Column<string[]>(type: "text[]", nullable: false),
                    backup_task_id = table.Column<int>(type: "integer", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_backup_snapshots_backup_tasks_backup_task_id",
                        column: x => x.backup_task_id,
                        principalTable: "backup_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_backup_snapshots_backup_task_id",
                table: "backup_snapshots",
                column: "backup_task_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backup_snapshots");

            migrationBuilder.RenameColumn(
                name: "last_message",
                table: "backup_tasks",
                newName: "last_error");
        }
    }
}
