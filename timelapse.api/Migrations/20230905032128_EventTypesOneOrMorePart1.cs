using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class EventTypesOneOrMorePart1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_event_type",
                columns: table => new
                {
                    event_types_id = table.Column<int>(type: "integer", nullable: false),
                    events_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_event_type", x => new { x.event_types_id, x.events_id });
                    table.ForeignKey(
                        name: "fk_event_event_type_event_types_event_types_id",
                        column: x => x.event_types_id,
                        principalTable: "event_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_event_type_events_events_id",
                        column: x => x.events_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_event_type_events_id",
                table: "event_event_type",
                column: "events_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_event_type");
        }
    }
}
