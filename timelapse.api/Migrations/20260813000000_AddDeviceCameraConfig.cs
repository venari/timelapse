using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class AddDeviceCameraConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "sleep_during_night",
                table: "devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "daytime_starts_at_h",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "daytime_ends_at_h",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 17);

            migrationBuilder.AddColumn<int>(
                name: "camera_interval_s",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 300);

            migrationBuilder.AddColumn<string>(
                name: "api_url",
                table: "devices",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sleep_during_night",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "daytime_starts_at_h",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "daytime_ends_at_h",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "camera_interval_s",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "api_url",
                table: "devices");
        }
    }
}
