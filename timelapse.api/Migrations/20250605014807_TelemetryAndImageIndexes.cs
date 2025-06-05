using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class TelemetryAndImageIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_telemetry_timestamp",
                table: "telemetry",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_images_timestamp",
                table: "images",
                column: "timestamp");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_telemetry_timestamp",
                table: "telemetry");

            migrationBuilder.DropIndex(
                name: "ix_images_timestamp",
                table: "images");
        }
    }
}
