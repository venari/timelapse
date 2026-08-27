using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceLongExposureConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "enable_long_exposure_at_night",
                table: "devices",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "long_exposure_xclk_hz",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 8000000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "enable_long_exposure_at_night",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "long_exposure_xclk_hz",
                table: "devices");
        }
    }
}
