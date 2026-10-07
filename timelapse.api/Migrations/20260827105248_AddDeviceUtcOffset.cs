using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceUtcOffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "utc_offset_minutes",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 720);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "utc_offset_minutes",
                table: "devices");
        }
    }
}
