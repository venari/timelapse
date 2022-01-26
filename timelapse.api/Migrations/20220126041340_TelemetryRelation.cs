using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class TelemetryRelation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_telemetry_device_id",
                table: "telemetry",
                column: "device_id");

            migrationBuilder.AddForeignKey(
                name: "fk_telemetry_devices_device_id",
                table: "telemetry",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_telemetry_devices_device_id",
                table: "telemetry");

            migrationBuilder.DropIndex(
                name: "ix_telemetry_device_id",
                table: "telemetry");
        }
    }
}
