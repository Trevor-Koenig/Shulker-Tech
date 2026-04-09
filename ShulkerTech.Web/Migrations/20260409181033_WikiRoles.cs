using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShulkerTech.Web.Migrations
{
    /// <inheritdoc />
    public partial class WikiRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EditRole",
                table: "Articles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewRole",
                table: "Articles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WikiSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DefaultViewRole = table.Column<string>(type: "text", nullable: true),
                    CreateRole = table.Column<string>(type: "text", nullable: false),
                    EditAnyRole = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "WikiSettings",
                columns: new[] { "Id", "CreateRole", "DefaultViewRole", "EditAnyRole" },
                values: new object[] { 1, "Member", null, "Moderator" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WikiSettings");

            migrationBuilder.DropColumn(
                name: "EditRole",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ViewRole",
                table: "Articles");
        }
    }
}
