using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class EventStartAndEndImage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "end_image_id",
                table: "events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "start_image_id",
                table: "events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_events_end_image_id",
                table: "events",
                column: "end_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_start_image_id",
                table: "events",
                column: "start_image_id");

            migrationBuilder.AddForeignKey(
                name: "fk_events_images_end_image_id",
                table: "events",
                column: "end_image_id",
                principalTable: "images",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_events_images_start_image_id",
                table: "events",
                column: "start_image_id",
                principalTable: "images",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_images_end_image_id",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "fk_events_images_start_image_id",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_end_image_id",
                table: "events");

            migrationBuilder.DropIndex(
                name: "ix_events_start_image_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "end_image_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "start_image_id",
                table: "events");
        }
    }
}
