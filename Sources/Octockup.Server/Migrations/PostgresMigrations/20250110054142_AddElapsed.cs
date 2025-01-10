using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddElapsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "elapsed",
                table: "backup_snapshots",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "elapsed",
                table: "backup_snapshots");
        }
    }
}
