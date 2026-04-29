using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShulkerTech.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleTemplates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ArticleTemplates",
                columns: ["Name", "Description", "Content", "IsDefault", "CreatedAt", "UpdatedAt"],
                values: new object[]
                {
                    "Point of Interest",
                    "Coordinates, map embed, gallery, and see-also for a notable in-game location.",
                    "## Introduction\n\n_Describe this location in a few sentences._\n\n## Coordinates\n\n| Dimension | X | Y | Z |\n|-----------|---|---|---|\n| Overworld |   |   |   |\n| Nether    |   |   |   |\n\n## Map\n\n```map\nhttps://map.shulkertech.com/#world:0:64:0:300:0:0:0:0:perspective\n```\n\n## Gallery\n\n_Upload screenshots of this location._\n\n## See Also\n\n- ",
                    true,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleTemplates");
        }
    }
}
