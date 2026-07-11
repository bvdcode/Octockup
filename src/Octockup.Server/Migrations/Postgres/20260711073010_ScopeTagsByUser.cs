using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ScopeTagsByUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_modules_tag",
                table: "modules");

            migrationBuilder.DropIndex(
                name: "IX_modules_user_id",
                table: "modules");

            migrationBuilder.DropIndex(
                name: "IX_backups_tag",
                table: "backups");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "backups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_modules_user_id_tag",
                table: "modules",
                columns: new[] { "user_id", "tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_backups_user_id_tag",
                table: "backups",
                columns: new[] { "user_id", "tag" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_modules_user_id_tag",
                table: "modules");

            migrationBuilder.DropIndex(
                name: "IX_backups_user_id_tag",
                table: "backups");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "backups");

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
                name: "IX_backups_tag",
                table: "backups",
                column: "tag",
                unique: true);
        }
    }
}
