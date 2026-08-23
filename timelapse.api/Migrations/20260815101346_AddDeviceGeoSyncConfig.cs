using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceGeoSyncConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "auto_sync_period_s",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 300);

            migrationBuilder.AddColumn<int>(
                name: "geo_interval_s",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 3600);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "auto_sync_period_s",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "geo_interval_s",
                table: "devices");
        }
    }
}
