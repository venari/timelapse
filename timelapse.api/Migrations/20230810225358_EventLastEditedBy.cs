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
                nullable: true,
                defaultValue: null);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_edited_date",
                table: "events",
                type: "timestamp with time zone",
                nullable: true,
                defaultValue: null);

            migrationBuilder.Sql("UPDATE events SET last_edited_by_user_id = created_by_user_id, last_edited_date = created_date WHERE last_edited_by_user_id IS NULL AND last_edited_date IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "last_edited_by_user_id",
                table: "events",
                nullable: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "last_edited_date",
                table: "events",
                nullable: false);

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
