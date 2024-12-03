using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "users",
                newName: "password_hash_sha512");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "role",
                table: "users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "password_hash_sha512",
                table: "users",
                newName: "password_hash");
        }
    }
}
