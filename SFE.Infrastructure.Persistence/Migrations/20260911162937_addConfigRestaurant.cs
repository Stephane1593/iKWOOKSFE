using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addConfigRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrinterProfileId",
                table: "MenuItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_PrinterProfileId",
                table: "MenuItems",
                column: "PrinterProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_PrinterProfiles_PrinterProfileId",
                table: "MenuItems",
                column: "PrinterProfileId",
                principalTable: "PrinterProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_PrinterProfiles_PrinterProfileId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_PrinterProfileId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "PrinterProfileId",
                table: "MenuItems");
        }
    }
}
