using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace timelapse.api.Migrations
{
    public partial class containers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "container_overide_id",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "containers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    owner_organisation_id = table.Column<int>(type: "integer", nullable: false),
                    discriminator = table.Column<string>(type: "text", nullable: false),
                    region = table.Column<string>(type: "text", nullable: true),
                    bucket_name = table.Column<string>(type: "text", nullable: true),
                    access_key = table.Column<string>(type: "text", nullable: true),
                    secret_key = table.Column<string>(type: "text", nullable: true),
                    container_name = table.Column<string>(type: "text", nullable: true),
                    connection_string = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_containers", x => x.id);
                    table.ForeignKey(
                        name: "fk_containers_organisations_owner_organisation_id",
                        column: x => x.owner_organisation_id,
                        principalTable: "organisations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_projects_container_overide_id",
                table: "projects",
                column: "container_overide_id");

            migrationBuilder.CreateIndex(
                name: "ix_containers_owner_organisation_id",
                table: "containers",
                column: "owner_organisation_id");

            migrationBuilder.AddForeignKey(
                name: "fk_projects_containers_container_overide_id",
                table: "projects",
                column: "container_overide_id",
                principalTable: "containers",
                principalColumn: "id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_projects_containers_container_overide_id",
                table: "projects");

            migrationBuilder.DropTable(
                name: "containers");

            migrationBuilder.DropIndex(
                name: "ix_projects_container_overide_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "container_overide_id",
                table: "projects");
        }
    }
}
