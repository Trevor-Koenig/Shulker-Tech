using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShulkerTech.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddMinecraftServerPolling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Host",
                table: "MinecraftServers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Port",
                table: "MinecraftServers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Host",
                table: "MinecraftServers");

            migrationBuilder.DropColumn(
                name: "Port",
                table: "MinecraftServers");
        }
    }
}
