using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class TelemetryOutputFolders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "pending_images",
                table: "telemetry",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pending_telemetry",
                table: "telemetry",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "uploaded_images",
                table: "telemetry",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "uploaded_telemetry",
                table: "telemetry",
                type: "integer",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pending_images",
                table: "telemetry");

            migrationBuilder.DropColumn(
                name: "pending_telemetry",
                table: "telemetry");

            migrationBuilder.DropColumn(
                name: "uploaded_images",
                table: "telemetry");

            migrationBuilder.DropColumn(
                name: "uploaded_telemetry",
                table: "telemetry");
        }
    }
}
