using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class RestrictSnapshotScheduleDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_snapshots_schedules_ScheduleId",
                table: "snapshots");

            migrationBuilder.AddForeignKey(
                name: "FK_snapshots_schedules_ScheduleId",
                table: "snapshots",
                column: "ScheduleId",
                principalTable: "schedules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_snapshots_schedules_ScheduleId",
                table: "snapshots");

            migrationBuilder.AddForeignKey(
                name: "FK_snapshots_schedules_ScheduleId",
                table: "snapshots",
                column: "ScheduleId",
                principalTable: "schedules",
                principalColumn: "id");
        }
    }
}
