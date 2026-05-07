using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShulkerTech.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestRole",
                table: "SecuritySettings",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SecuritySettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "GuestRole",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestRole",
                table: "SecuritySettings");
        }
    }
}
