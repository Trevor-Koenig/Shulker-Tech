using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShulkerTech.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleMapUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MapUrl",
                table: "Articles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MapUrl",
                table: "Articles");
        }
    }
}
