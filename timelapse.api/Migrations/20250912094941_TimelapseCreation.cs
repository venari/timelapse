using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    /// <inheritdoc />
    public partial class TimelapseCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_location_devices_device_id",
                table: "device_location");

            migrationBuilder.DropForeignKey(
                name: "fk_event_comment_events_event_id",
                table: "event_comment");

            migrationBuilder.DropForeignKey(
                name: "fk_event_event_type_event_types_event_types_id",
                table: "event_event_type");

            migrationBuilder.DropForeignKey(
                name: "fk_event_event_type_events_events_id",
                table: "event_event_type");

            migrationBuilder.DropForeignKey(
                name: "fk_organisation_user_join_entry_organisations_organisation_id",
                table: "organisation_user_join_entry");

            migrationBuilder.DropForeignKey(
                name: "fk_projects_organisations_organisation_id",
                table: "projects");

            migrationBuilder.DropForeignKey(
                name: "fk_telemetry_devices_device_id",
                table: "telemetry");

            migrationBuilder.AddColumn<DateTime>(
                name: "timelapse_created_date",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "timelapse_status",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "timelapse_url",
                table: "events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "fk_device_location_device_device_id",
                table: "device_location",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_event_comment_event_event_id",
                table: "event_comment",
                column: "event_id",
                principalTable: "events",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_event_event_type_event_events_id",
                table: "event_event_type",
                column: "events_id",
                principalTable: "events",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_event_event_type_event_type_event_types_id",
                table: "event_event_type",
                column: "event_types_id",
                principalTable: "event_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_organisation_user_join_entry_organisation_organisation_id",
                table: "organisation_user_join_entry",
                column: "organisation_id",
                principalTable: "organisations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_projects_organisation_organisation_id",
                table: "projects",
                column: "organisation_id",
                principalTable: "organisations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_telemetry_device_device_id",
                table: "telemetry",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_location_device_device_id",
                table: "device_location");

            migrationBuilder.DropForeignKey(
                name: "fk_event_comment_event_event_id",
                table: "event_comment");

            migrationBuilder.DropForeignKey(
                name: "fk_event_event_type_event_events_id",
                table: "event_event_type");

            migrationBuilder.DropForeignKey(
                name: "fk_event_event_type_event_type_event_types_id",
                table: "event_event_type");

            migrationBuilder.DropForeignKey(
                name: "fk_organisation_user_join_entry_organisation_organisation_id",
                table: "organisation_user_join_entry");

            migrationBuilder.DropForeignKey(
                name: "fk_projects_organisation_organisation_id",
                table: "projects");

            migrationBuilder.DropForeignKey(
                name: "fk_telemetry_device_device_id",
                table: "telemetry");

            migrationBuilder.DropColumn(
                name: "timelapse_created_date",
                table: "events");

            migrationBuilder.DropColumn(
                name: "timelapse_status",
                table: "events");

            migrationBuilder.DropColumn(
                name: "timelapse_url",
                table: "events");

            migrationBuilder.AddForeignKey(
                name: "fk_device_location_devices_device_id",
                table: "device_location",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_event_comment_events_event_id",
                table: "event_comment",
                column: "event_id",
                principalTable: "events",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_event_event_type_event_types_event_types_id",
                table: "event_event_type",
                column: "event_types_id",
                principalTable: "event_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_event_event_type_events_events_id",
                table: "event_event_type",
                column: "events_id",
                principalTable: "events",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_organisation_user_join_entry_organisations_organisation_id",
                table: "organisation_user_join_entry",
                column: "organisation_id",
                principalTable: "organisations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_projects_organisations_organisation_id",
                table: "projects",
                column: "organisation_id",
                principalTable: "organisations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_telemetry_devices_device_id",
                table: "telemetry",
                column: "device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
