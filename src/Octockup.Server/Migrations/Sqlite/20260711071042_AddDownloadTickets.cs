using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddDownloadTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "download_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    kind = table.Column<int>(type: "INTEGER", nullable: false),
                    resource_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    secondary_resource_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    include_files = table.Column<bool>(type: "INTEGER", nullable: false),
                    expires_at = table.Column<string>(type: "TEXT", nullable: false),
                    consumed_at = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_download_tickets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_download_tickets_expires_at",
                table: "download_tickets",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_download_tickets_token_hash",
                table: "download_tickets",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "download_tickets");
        }
    }
}
