using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignDailyReportForDGI2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPeriodic = table.Column<bool>(type: "INTEGER", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CompanyNIF = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ISF = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OperatorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PointOfSaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalInvoiceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IncompleteCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GrandTotalHT = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotalTVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotalTTC = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalSpecificTax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrintContent = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArticleReportLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticleCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ArticleName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TaxGroup = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantitySold = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    QuantityInStock = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleReportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleReportLines_DailyReports_DailyReportId",
                        column: x => x.DailyReportId,
                        principalTable: "DailyReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportInvoiceTypeSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceType = table.Column<int>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalHT = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTTC = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalSpecificTax = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportInvoiceTypeSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportInvoiceTypeSummaries_DailyReports_DailyReportId",
                        column: x => x.DailyReportId,
                        principalTable: "DailyReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportPaymentSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentType = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportPaymentSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportPaymentSummaries_DailyReports_DailyReportId",
                        column: x => x.DailyReportId,
                        principalTable: "DailyReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportTaxGroupDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DailyReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceType = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxGroup = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportTaxGroupDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportTaxGroupDetails_DailyReports_DailyReportId",
                        column: x => x.DailyReportId,
                        principalTable: "DailyReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleReportLines_DailyReportId",
                table: "ArticleReportLines",
                column: "DailyReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportInvoiceTypeSummaries_DailyReportId",
                table: "ReportInvoiceTypeSummaries",
                column: "DailyReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportPaymentSummaries_DailyReportId",
                table: "ReportPaymentSummaries",
                column: "DailyReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportTaxGroupDetails_DailyReportId",
                table: "ReportTaxGroupDetails",
                column: "DailyReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleReportLines");

            migrationBuilder.DropTable(
                name: "ReportInvoiceTypeSummaries");

            migrationBuilder.DropTable(
                name: "ReportPaymentSummaries");

            migrationBuilder.DropTable(
                name: "ReportTaxGroupDetails");

            migrationBuilder.DropTable(
                name: "DailyReports");
        }
    }
}
