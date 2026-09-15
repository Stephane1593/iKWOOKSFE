using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addKitchenRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Port",
                table: "PrinterProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrinterProfileId",
                table: "Menus",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Menus_PrinterProfileId",
                table: "Menus",
                column: "PrinterProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Menus_PrinterProfiles_PrinterProfileId",
                table: "Menus",
                column: "PrinterProfileId",
                principalTable: "PrinterProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Menus_PrinterProfiles_PrinterProfileId",
                table: "Menus");

            migrationBuilder.DropIndex(
                name: "IX_Menus_PrinterProfileId",
                table: "Menus");

            migrationBuilder.DropColumn(
                name: "Port",
                table: "PrinterProfiles");

            migrationBuilder.DropColumn(
                name: "PrinterProfileId",
                table: "Menus");
        }
    }
}
