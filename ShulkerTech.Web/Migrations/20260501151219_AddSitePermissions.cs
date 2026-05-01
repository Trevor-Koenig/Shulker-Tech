using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ShulkerTech.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSitePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreateRole",
                table: "WikiSettings");

            migrationBuilder.DropColumn(
                name: "EditAnyRole",
                table: "WikiSettings");

            migrationBuilder.CreateTable(
                name: "SitePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleName = table.Column<string>(type: "text", nullable: false),
                    Resource = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitePermissions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SitePermissions",
                columns: new[] { "Id", "Resource", "RoleName" },
                values: new object[,]
                {
                    { 1, "wiki.create", "Member" },
                    { 2, "wiki.edit_own", "Member" },
                    { 3, "wiki.create", "Moderator" },
                    { 4, "wiki.edit_own", "Moderator" },
                    { 5, "wiki.edit_any", "Moderator" },
                    { 6, "wiki.delete", "Moderator" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SitePermissions_RoleName_Resource",
                table: "SitePermissions",
                columns: new[] { "RoleName", "Resource" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SitePermissions");

            migrationBuilder.AddColumn<string>(
                name: "CreateRole",
                table: "WikiSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EditAnyRole",
                table: "WikiSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "WikiSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreateRole", "EditAnyRole" },
                values: new object[] { "Member", "Moderator" });
        }
    }
}
