using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShulkerTech.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HeroTagline = table.Column<string>(type: "text", nullable: false),
                    BuildCardTitle = table.Column<string>(type: "text", nullable: false),
                    BuildCardBody = table.Column<string>(type: "text", nullable: false),
                    ExploreCardTitle = table.Column<string>(type: "text", nullable: false),
                    ExploreCardBody = table.Column<string>(type: "text", nullable: false),
                    ConnectCardTitle = table.Column<string>(type: "text", nullable: false),
                    ConnectCardBody = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SiteSettings",
                columns: new[] { "Id", "BuildCardBody", "BuildCardTitle", "ConnectCardBody", "ConnectCardTitle", "ExploreCardBody", "ExploreCardTitle", "HeroTagline" },
                values: new object[] { 1, "From mega-farms to redstone contraptions, Shulker Tech celebrates technical Minecraft. Build what others say is impossible.", "BUILD", "A tight-knit community of builders and tinkerers. Find your people, share your farms, and shape the server together.", "CONNECT", "Discover automated systems, player-built infrastructure, and the remnants of seasons past.", "EXPLORE", "A technical Minecraft community — engineered by its players, built to last." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteSettings");
        }
    }
}
