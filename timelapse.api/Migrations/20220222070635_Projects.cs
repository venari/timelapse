using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class Projects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_placement_project_project_id",
                table: "device_placement");

            migrationBuilder.DropForeignKey(
                name: "fk_project_user_project_project_id",
                table: "project_user");

            migrationBuilder.DropPrimaryKey(
                name: "pk_project",
                table: "project");

            migrationBuilder.RenameTable(
                name: "project",
                newName: "projects");

            migrationBuilder.AddPrimaryKey(
                name: "pk_projects",
                table: "projects",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_device_placement_projects_project_id",
                table: "device_placement",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_project_user_projects_project_id",
                table: "project_user",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_placement_projects_project_id",
                table: "device_placement");

            migrationBuilder.DropForeignKey(
                name: "fk_project_user_projects_project_id",
                table: "project_user");

            migrationBuilder.DropPrimaryKey(
                name: "pk_projects",
                table: "projects");

            migrationBuilder.RenameTable(
                name: "projects",
                newName: "project");

            migrationBuilder.AddPrimaryKey(
                name: "pk_project",
                table: "project",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_device_placement_project_project_id",
                table: "device_placement",
                column: "project_id",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_project_user_project_project_id",
                table: "project_user",
                column: "project_id",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
