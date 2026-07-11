using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddStorageOperationLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "active_storage_operation_id",
                table: "modules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "active_storage_operation_kind",
                table: "modules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "storage_operation_lease_expires_at",
                table: "modules",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "active_storage_operation_id",
                table: "modules");

            migrationBuilder.DropColumn(
                name: "active_storage_operation_kind",
                table: "modules");

            migrationBuilder.DropColumn(
                name: "storage_operation_lease_expires_at",
                table: "modules");
        }
    }
}
