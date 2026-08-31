using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExchangeRateMode = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentExchangeRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrentExchangeRateEUR = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrentExchangeRateCNY = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ExchangeRateUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DefaultCurrency = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultPriceMode = table.Column<string>(type: "TEXT", nullable: false),
                    DiscountBeforeTax = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CompanyNIF = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CompanyRCCM = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CompanyIdNat = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CompanyAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CompanyPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CompanyEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Module = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CodeDEFDGI = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    PointOfSaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    PointOfSaleName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    NIF = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RCCM = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsLoyaltyMember = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NIF = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ISF = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    RCCM = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Logo = table.Column<byte[]>(type: "BLOB", nullable: true),
                    DefaultPriceMode = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LoyaltyEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LoyaltyEarnRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 1000m),
                    LoyaltyRedeemRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 500m),
                    DeploymentMode = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

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
                    PrintContent = table.Column<string>(type: "TEXT", nullable: true),
                    SessionOpenedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OpeningAmountUSD = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    OpeningAmountCDF = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    OpeningAmountEUR = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    OpeningAmountCNY = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    RateUSD = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    RateEUR = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    RateCNY = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    OpeningNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ClosingAmountUSD = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ClosingAmountCDF = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ClosingAmountEUR = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ClosingAmountCNY = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ClosingNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ExpectedCashUSD = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ExpectedCashCDF = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ExpectedCashEUR = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    ExpectedCashCNY = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    VarianceUSD = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    VarianceCDF = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    VarianceEUR = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    VarianceCNY = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PriceMode = table.Column<int>(type: "INTEGER", nullable: false),
                    ISF = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ClientType = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientNIF = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ClientName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ClientAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ClientPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ClientEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ClientRCCM = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OperatorId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OperatorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OriginalInvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreditNoteNature = table.Column<int>(type: "INTEGER", nullable: true),
                    OriginalInvoiceReference = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ReferenceType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ReferenceDesc = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AdvanceGroupId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    OrderTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ParentInvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CurrencyRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    CurrencyDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CommentA = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CommentB = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CommentC = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CommentD = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CommentE = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CommentF = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CommentG = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CommentH = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TotalHTBeforeDiscount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalTVA = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalSpecificTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalFixedSpecificTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalPercentSpecificTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalAdvancesPaid = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RemainingBalance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PreviousAdvancesTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RemainingAfterAdvance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ConvertedToInvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProformaValidUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SourceProformaId = table.Column<int>(type: "INTEGER", nullable: true),
                    DiscountBeforeTax = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmcfUid = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CodeDEFDGI = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    QRCodeContent = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NIM = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Counters = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DeviceDateTime = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NormalizedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PointOfSaleId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Invoices_ConvertedToInvoiceId",
                        column: x => x.ConvertedToInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invoices_Invoices_OriginalInvoiceId",
                        column: x => x.OriginalInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Invoices_ParentInvoiceId",
                        column: x => x.ParentInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Invoices_SourceProformaId",
                        column: x => x.SourceProformaId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Permissions = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TotalPointsEarned = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentBalance = table.Column<int>(type: "INTEGER", nullable: false),
                    TierLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyAccounts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PointsOfSale",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeviceType = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    EmcfApiUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EmcfToken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    EmcfNIM = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    McfPortName = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    McfBaudRate = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 115200),
                    LastConnectionTestAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastKnownNIM = table.Column<string>(type: "TEXT", nullable: true),
                    LastKnownNIF = table.Column<string>(type: "TEXT", nullable: true),
                    McfLastServerConnection = table.Column<DateTime>(type: "TEXT", nullable: true),
                    McfServerStatus = table.Column<string>(type: "TEXT", nullable: true),
                    ManagesStock = table.Column<bool>(type: "INTEGER", nullable: false),
                    ThermalPrinterName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    PaperWidthMm = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 80),
                    AutoPrintReceipt = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    PrintCopies = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    EnableCustomerDisplay = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    EnableCashDrawer = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CashDrawerPin = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    PrinterCodePage = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 858),
                    PrintLogo = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ReceiptFooterText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: "Merci pour votre achat !"),
                    AllowNegativeStock = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsOfSale", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointsOfSale_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ItemType = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxGroup = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPriceHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPriceTTC = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PriceModification = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DiscountType = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    HasSpecificTax = table.Column<bool>(type: "INTEGER", nullable: false),
                    SpecificTaxName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SpecificTaxRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    TaxApplicationMode = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxSpecificValue = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TaxSpecificAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SpecificTaxType = table.Column<int>(type: "INTEGER", nullable: false),
                    SpecificTaxValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    AmountHTBeforeDiscount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AmountHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AmountTVA = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AmountTTC = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoicePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentType = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    CurrencyRate = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePayments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ItemType = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxGroup = table.Column<int>(type: "INTEGER", nullable: false),
                    SpecificTaxType = table.Column<int>(type: "INTEGER", nullable: false),
                    SpecificTaxValue = table.Column<decimal>(type: "TEXT", maxLength: 30, nullable: false),
                    TaxSpecificMode = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StockQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    MinStockLevel = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    TrackStock = table.Column<bool>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UnitPriceHtCdf = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPriceTtcCdf = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPriceHtUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    UnitPriceTtcUsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    DefaultDiscountType = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultDiscountValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ProductCategoryId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Products_ProductCategories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LoyaltyAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_LoyaltyAccounts_LoyaltyAccountId",
                        column: x => x.LoyaltyAccountId,
                        principalTable: "LoyaltyAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransferNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 25, nullable: false),
                    FromPointOfSaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToPointOfSaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ReceivedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ShippedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransfers_PointsOfSale_FromPointOfSaleId",
                        column: x => x.FromPointOfSaleId,
                        principalTable: "PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_PointsOfSale_ToPointOfSaleId",
                        column: x => x.ToPointOfSaleId,
                        principalTable: "PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PointOfSaleId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_PointsOfSale_PointOfSaleId",
                        column: x => x.PointOfSaleId,
                        principalTable: "PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosStocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    PointOfSaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    MinStockLevel = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    MaxStockLevel = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    LastMovementAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PosStocks_PointsOfSale_PointOfSaleId",
                        column: x => x.PointOfSaleId,
                        principalTable: "PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PosStocks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    PointOfSaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    QuantityBefore = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CounterpartPointOfSaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    TransferReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    OperatorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_PointsOfSale_PointOfSaleId",
                        column: x => x.PointOfSaleId,
                        principalTable: "PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StockTransferId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_StockTransfers_StockTransferId",
                        column: x => x.StockTransferId,
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "CompanyAddress", "CompanyEmail", "CompanyIdNat", "CompanyNIF", "CompanyName", "CompanyPhone", "CompanyRCCM", "CurrentExchangeRate", "CurrentExchangeRateCNY", "CurrentExchangeRateEUR", "DefaultCurrency", "DefaultPriceMode", "DiscountBeforeTax", "ExchangeRateMode", "ExchangeRateUpdatedAt", "UpdatedAt" },
                values: new object[] { 1, "", "", "", "", "", "", "", 2800m, 385m, 3100m, "CDF", "TTC", true, "Manual", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleReportLines_DailyReportId",
                table: "ArticleReportLines",
                column: "DailyReportId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Action",
                table: "AuditLog",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CodeDEF",
                table: "AuditLog",
                column: "CodeDEFDGI");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_InvNum",
                table: "AuditLog",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Module",
                table: "AuditLog",
                column: "Module");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp",
                table: "AuditLog",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp_Module",
                table: "AuditLog",
                columns: new[] { "Timestamp", "Module" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_UserId",
                table: "AuditLog",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name",
                table: "Clients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_NIF",
                table: "Clients",
                column: "NIF");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_ArticleId",
                table: "InvoiceLines",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId_LineNumber",
                table: "InvoiceLines",
                columns: new[] { "InvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_ProductId",
                table: "InvoiceLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_InvoiceId",
                table: "InvoicePayments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_AdvanceGroupId",
                table: "Invoices",
                column: "AdvanceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ConvertedToInvoiceId",
                table: "Invoices",
                column: "ConvertedToInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedAt",
                table: "Invoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OriginalInvoiceId",
                table: "Invoices",
                column: "OriginalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ParentInvoiceId",
                table: "Invoices",
                column: "ParentInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SourceProformaId",
                table: "Invoices",
                column: "SourceProformaId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Type",
                table: "Invoices",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Type_ConvertedToInvoiceId",
                table: "Invoices",
                columns: new[] { "Type", "ConvertedToInvoiceId" },
                filter: "[Type] = 6");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAccounts_CardNumber",
                table: "LoyaltyAccounts",
                column: "CardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyAccounts_ClientId",
                table: "LoyaltyAccounts",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_LoyaltyAccountId",
                table: "LoyaltyTransactions",
                column: "LoyaltyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_Timestamp",
                table: "LoyaltyTransactions",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_PointsOfSale_Code",
                table: "PointsOfSale",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointsOfSale_CompanyId",
                table: "PointsOfSale",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PosStock_Pos",
                table: "PosStocks",
                column: "PointOfSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PosStock_Product_Pos",
                table: "PosStocks",
                columns: new[] { "ProductId", "PointOfSaleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_Name",
                table: "ProductCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                table: "Products",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive",
                table: "Products",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsFavorite",
                table: "Products",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductCategoryId",
                table: "Products",
                column: "ProductCategoryId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PointOfSaleId",
                table: "StockMovements",
                column: "PointOfSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMvt_Date",
                table: "StockMovements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockMvt_Product_Pos_Date",
                table: "StockMovements",
                columns: new[] { "ProductId", "PointOfSaleId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMvt_Reference",
                table: "StockMovements",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_StockMvt_TransferRef",
                table: "StockMovements",
                column: "TransferReference");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_ProductId",
                table: "StockTransferLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_StockTransferId",
                table: "StockTransferLines",
                column: "StockTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_FromPointOfSaleId",
                table: "StockTransfers",
                column: "FromPointOfSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_Status",
                table: "StockTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ToPointOfSaleId",
                table: "StockTransfers",
                column: "ToPointOfSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_TransferNumber",
                table: "StockTransfers",
                column: "TransferNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PointOfSaleId",
                table: "Users",
                column: "PointOfSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ArticleReportLines");

            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "InvoicePayments");

            migrationBuilder.DropTable(
                name: "LoyaltyTransactions");

            migrationBuilder.DropTable(
                name: "PosStocks");

            migrationBuilder.DropTable(
                name: "ReportInvoiceTypeSummaries");

            migrationBuilder.DropTable(
                name: "ReportPaymentSummaries");

            migrationBuilder.DropTable(
                name: "ReportTaxGroupDetails");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockTransferLines");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "LoyaltyAccounts");

            migrationBuilder.DropTable(
                name: "DailyReports");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "PointsOfSale");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
