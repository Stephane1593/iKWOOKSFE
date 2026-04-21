using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterConfigToPos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoPrintReceipt",
                table: "PointsOfSale",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CashDrawerPin",
                table: "PointsOfSale",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EnableCashDrawer",
                table: "PointsOfSale",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableCustomerDisplay",
                table: "PointsOfSale",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PaperWidthMm",
                table: "PointsOfSale",
                type: "INTEGER",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<int>(
                name: "PrintCopies",
                table: "PointsOfSale",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "PrintLogo",
                table: "PointsOfSale",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PrinterCodePage",
                table: "PointsOfSale",
                type: "INTEGER",
                nullable: false,
                defaultValue: 858);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptFooterText",
                table: "PointsOfSale",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "Merci pour votre achat !");

            migrationBuilder.AddColumn<string>(
                name: "ThermalPrinterName",
                table: "PointsOfSale",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ClosingAmountCDF",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosingAmountCNY",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosingAmountEUR",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosingAmountUSD",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ClosingNotes",
                table: "DailyReports",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedCashCDF",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedCashCNY",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedCashEUR",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedCashUSD",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningAmountCDF",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningAmountCNY",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningAmountEUR",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningAmountUSD",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OpeningNotes",
                table: "DailyReports",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RateCNY",
                table: "DailyReports",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RateEUR",
                table: "DailyReports",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RateUSD",
                table: "DailyReports",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "SessionOpenedAt",
                table: "DailyReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VarianceCDF",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VarianceCNY",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VarianceEUR",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VarianceUSD",
                table: "DailyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoPrintReceipt",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "CashDrawerPin",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "EnableCashDrawer",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "EnableCustomerDisplay",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "PaperWidthMm",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "PrintCopies",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "PrintLogo",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "PrinterCodePage",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "ReceiptFooterText",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "ThermalPrinterName",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "ClosingAmountCDF",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "ClosingAmountCNY",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "ClosingAmountEUR",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "ClosingAmountUSD",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "ClosingNotes",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "ExpectedCashCDF",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "ExpectedCashCNY",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "ExpectedCashEUR",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "ExpectedCashUSD",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "OpeningAmountCDF",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "OpeningAmountCNY",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "OpeningAmountEUR",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "OpeningAmountUSD",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "OpeningNotes",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "RateCNY",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "RateEUR",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "RateUSD",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "SessionOpenedAt",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "VarianceCDF",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "VarianceCNY",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "VarianceEUR",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "VarianceUSD",
                table: "DailyReports");
        }
    }
}
