using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class addInvoicePointsRedeemed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsRedeemed",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyEarnRate",
                table: "AppSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "LoyaltyEnabled",
                table: "AppSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyMinRedeemPoints",
                table: "AppSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyRedeemRate",
                table: "AppSettings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LoyaltyEarnRate", "LoyaltyEnabled", "LoyaltyMinRedeemPoints", "LoyaltyRedeemRate" },
                values: new object[] { 1000m, false, 100, 10m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointsRedeemed",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LoyaltyEarnRate",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyEnabled",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyMinRedeemPoints",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LoyaltyRedeemRate",
                table: "AppSettings");
        }
    }
}
