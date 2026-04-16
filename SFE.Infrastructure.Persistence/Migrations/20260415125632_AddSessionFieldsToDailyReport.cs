using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionFieldsToDailyReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentExchangeRateCNY",
                table: "AppSettings",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentExchangeRateEUR",
                table: "AppSettings",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CurrentExchangeRateCNY", "CurrentExchangeRateEUR" },
                values: new object[] { 385m, 3100m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentExchangeRateCNY",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "CurrentExchangeRateEUR",
                table: "AppSettings");
        }
    }
}
