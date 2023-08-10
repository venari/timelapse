using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class EventLastEditedBy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_edited_by_user_id",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue:  );

            migrationBuilder.AddColumn<DateTime>(
                name: "last_edited_date",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_edited_by_user_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "last_edited_date",
                table: "events");
        }
    }
}
