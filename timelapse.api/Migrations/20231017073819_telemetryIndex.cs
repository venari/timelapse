using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class telemetryIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_telemetry_timestamp",
                table: "telemetry",
                column: "timestamp");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_telemetry_timestamp",
                table: "telemetry");
        }
    }
}
