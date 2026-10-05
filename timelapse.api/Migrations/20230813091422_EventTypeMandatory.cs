using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class EventTypeMandatory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_event_types_event_type_id",
                table: "events");

            migrationBuilder.AlterColumn<int>(
                name: "event_type_id",
                table: "events",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_events_event_types_event_type_id",
                table: "events",
                column: "event_type_id",
                principalTable: "event_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_event_types_event_type_id",
                table: "events");

            migrationBuilder.AlterColumn<int>(
                name: "event_type_id",
                table: "events",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "fk_events_event_types_event_type_id",
                table: "events",
                column: "event_type_id",
                principalTable: "event_types",
                principalColumn: "id");
        }
    }
}
