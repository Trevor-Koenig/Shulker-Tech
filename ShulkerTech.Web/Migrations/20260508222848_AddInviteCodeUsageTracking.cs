using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShulkerTech.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteCodeUsageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RedeemedAt",
                table: "InviteCodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedeemedById",
                table: "InviteCodes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_RedeemedById",
                table: "InviteCodes",
                column: "RedeemedById");

            migrationBuilder.AddForeignKey(
                name: "FK_InviteCodes_AspNetUsers_RedeemedById",
                table: "InviteCodes",
                column: "RedeemedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InviteCodes_AspNetUsers_RedeemedById",
                table: "InviteCodes");

            migrationBuilder.DropIndex(
                name: "IX_InviteCodes_RedeemedById",
                table: "InviteCodes");

            migrationBuilder.DropColumn(
                name: "RedeemedAt",
                table: "InviteCodes");

            migrationBuilder.DropColumn(
                name: "RedeemedById",
                table: "InviteCodes");
        }
    }
}
