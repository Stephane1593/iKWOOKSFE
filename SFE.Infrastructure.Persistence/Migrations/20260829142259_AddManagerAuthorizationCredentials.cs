using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerAuthorizationCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagerBarcodeHash",
                table: "Users",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerPinHash",
                table: "Users",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerBarcodeHash",
                table: "Users",
                column: "ManagerBarcodeHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_ManagerBarcodeHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ManagerBarcodeHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ManagerPinHash",
                table: "Users");
        }
    }
}
