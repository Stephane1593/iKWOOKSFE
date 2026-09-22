using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExchangeRateMode = table.Column<string>(type: "text", nullable: false),
                    CurrentExchangeRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrentExchangeRateEUR = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrentExchangeRateCNY = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExchangeRateUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DefaultCurrency = table.Column<string>(type: "text", nullable: false),
                    DefaultPriceMode = table.Column<string>(type: "text", nullable: false),
                    DiscountBeforeTax = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyNIF = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompanyRCCM = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompanyIdNat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompanyAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CompanyPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompanyEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Module = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CodeDEFDGI = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: true),
                    PointOfSaleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    NIF = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RCCM = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsLoyaltyMember = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NIF = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ISF = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    RCCM = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    DefaultPriceMode = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LoyaltyEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LoyaltyEarnRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 1000m),
                    LoyaltyRedeemRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 500m),
                    DeploymentMode = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ReportNumber = table.Column<int>(type: "integer", nullable: false),
                    IsPeriodic = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyNIF = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ISF = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OperatorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    TotalInvoiceCount = table.Column<int>(type: "integer", nullable: false),
                    TotalItemCount = table.Column<int>(type: "integer", nullable: false),
                    IncompleteCount = table.Column<int>(type: "integer", nullable: false),
                    GrandTotalHT = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrandTotalTVA = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GrandTotalTTC = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalSpecificTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PrintContent = table.Column<string>(type: "text", nullable: true),
                    SessionOpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OpeningAmountUSD = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    OpeningAmountCDF = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    OpeningAmountEUR = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    OpeningAmountCNY = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    RateUSD = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    RateEUR = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    RateCNY = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    OpeningNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClosingAmountUSD = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    ClosingAmountCDF = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    ClosingAmountEUR = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    ClosingAmountCNY = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    ClosingNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpectedCashUSD = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    ExpectedCashCDF = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    ExpectedCashEUR = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    ExpectedCashCNY = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    VarianceUSD = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    VarianceCDF = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    VarianceEUR = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    VarianceCNY = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PriceMode = table.Column<int>(type: "integer", nullable: false),
                    ISF = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClientType = table.Column<int>(type: "integer", nullable: false),
                    ClientNIF = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClientName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ClientAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ClientPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClientEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientRCCM = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OperatorId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OperatorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    CreditNoteNature = table.Column<int>(type: "integer", nullable: true),
                    OriginalInvoiceReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReferenceDesc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AdvanceGroupId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    OrderTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ParentInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrencyRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CommentA = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CommentB = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CommentC = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CommentD = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CommentE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CommentF = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CommentG = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CommentH = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TotalHTBeforeDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalHT = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTVA = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSpecificTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalFixedSpecificTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPercentSpecificTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAdvancesPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RemainingBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousAdvancesTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RemainingAfterAdvance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConvertedToInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    ProformaValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceProformaId = table.Column<int>(type: "integer", nullable: true),
                    DiscountBeforeTax = table.Column<bool>(type: "boolean", nullable: false),
                    EmcfUid = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodeDEFDGI = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QRCodeContent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NIM = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Counters = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DeviceDateTime = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NormalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    PrintCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FirstPrintedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastPrintedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiningOption = table.Column<int>(type: "integer", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
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
                name: "PaymentTransactions",
                columns: table => new
                {
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<string>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderRef = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.IdempotencyKey);
                });

            migrationBuilder.CreateTable(
                name: "PrinterProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConnectionString = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    IsDefaultKitchen = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDefaultReceipt = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OriginPointOfSaleId = table.Column<int>(type: "integer", nullable: true),
                    OriginPointOfSaleSyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrinterProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Icon = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OriginPointOfSaleId = table.Column<int>(type: "integer", nullable: true),
                    OriginPointOfSaleSyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Permissions = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<int>(type: "integer", nullable: false),
                    CardNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalPointsEarned = table.Column<int>(type: "integer", nullable: false),
                    CurrentBalance = table.Column<int>(type: "integer", nullable: false),
                    TierLevel = table.Column<int>(type: "integer", nullable: false),
                    EnrolledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeviceType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EmcfApiUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EmcfToken = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EmcfNIM = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    McfPortName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    McfBaudRate = table.Column<int>(type: "integer", nullable: false, defaultValue: 115200),
                    DisableFallback = table.Column<bool>(type: "boolean", nullable: false),
                    LastConnectionTestAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastKnownNIM = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastKnownNIF = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    McfLastServerConnection = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    McfServerStatus = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ManagesStock = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsRestaurant = table.Column<bool>(type: "boolean", nullable: false),
                    ThermalPrinterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    PaperWidthMm = table.Column<int>(type: "integer", nullable: false, defaultValue: 80),
                    AutoPrintReceipt = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PrintCopies = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    EnableCustomerDisplay = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EnableCashDrawer = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CashDrawerPin = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PrinterCodePage = table.Column<int>(type: "integer", nullable: false, defaultValue: 858),
                    PrintLogo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReceiptFooterText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, defaultValue: "Merci pour votre achat !"),
                    AllowNegativeStock = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SunmiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SunmiTerminalUrl = table.Column<string>(type: "text", nullable: true),
                    SunmiTerminalId = table.Column<string>(type: "text", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DailyReportId = table.Column<int>(type: "integer", nullable: false),
                    ArticleCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ArticleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TaxGroup = table.Column<int>(type: "integer", nullable: false),
                    QuantitySold = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    QuantityInStock = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DailyReportId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceType = table.Column<int>(type: "integer", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    TotalHT = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTVA = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTTC = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalSpecificTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DailyReportId = table.Column<int>(type: "integer", nullable: false),
                    PaymentType = table.Column<int>(type: "integer", nullable: false),
                    InvoiceCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DailyReportId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceType = table.Column<int>(type: "integer", nullable: false),
                    TaxGroup = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxableAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    TaxGroup = table.Column<int>(type: "integer", nullable: false),
                    TaxGroupAType = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    TaxRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceHT = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceTTC = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PriceModification = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DiscountType = table.Column<int>(type: "integer", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HasSpecificTax = table.Column<bool>(type: "boolean", nullable: false),
                    SpecificTaxName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SpecificTaxRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxApplicationMode = table.Column<int>(type: "integer", nullable: false),
                    TaxSpecificValue = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TaxSpecificAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SpecificTaxType = table.Column<int>(type: "integer", nullable: false),
                    SpecificTaxValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossAmountHT = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossAmountTTC = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AmountHTBeforeDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountHT = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountTVA = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountTTC = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    PaymentType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CurrencyRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AuthCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Rrn = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    MaskedPan = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    CardScheme = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TerminalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TransactionRef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MobileOperator = table.Column<string>(type: "text", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    TaxGroup = table.Column<int>(type: "integer", nullable: false),
                    TaxGroupAType = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    SpecificTaxType = table.Column<int>(type: "integer", nullable: false),
                    SpecificTaxValue = table.Column<decimal>(type: "numeric", maxLength: 30, nullable: false),
                    TaxSpecificMode = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StockQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    MinStockLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    TrackStock = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UnitPriceHtCdf = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceTtcCdf = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceHtUsd = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPriceTtcUsd = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DefaultDiscountType = table.Column<int>(type: "integer", nullable: false),
                    DefaultDiscountValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ProductCategoryId = table.Column<int>(type: "integer", nullable: true)
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
                name: "Menus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PrinterProfileId = table.Column<int>(type: "integer", nullable: true),
                    SyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OriginPointOfSaleId = table.Column<int>(type: "integer", nullable: true),
                    OriginPointOfSaleSyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Menus_PrinterProfiles_PrinterProfileId",
                        column: x => x.PrinterProfileId,
                        principalTable: "PrinterProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Menus_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OriginPointOfSaleId = table.Column<int>(type: "integer", nullable: true),
                    OriginPointOfSaleSyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tables_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoyaltyAccountId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceId = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransferNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    FromPointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    ToPointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ShippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ManagerPinHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ManagerBarcodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ManagerBarcodeIssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinStockLevel = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaxStockLevel = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LastMovementAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityBefore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CounterpartPointOfSaleId = table.Column<int>(type: "integer", nullable: true),
                    TransferReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    OperatorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MenuId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CustomPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PrinterProfileId = table.Column<int>(type: "integer", nullable: true),
                    SyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OriginPointOfSaleId = table.Column<int>(type: "integer", nullable: true),
                    OriginPointOfSaleSyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_Menus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItems_PrinterProfiles_PrinterProfileId",
                        column: x => x.PrinterProfileId,
                        principalTable: "PrinterProfiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MenuItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    DiningOption = table.Column<int>(type: "integer", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OperatorId = table.Column<string>(type: "text", nullable: false),
                    WaiterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalHT = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTVA = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTTC = table.Column<decimal>(type: "numeric", nullable: false),
                    SyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    OriginPointOfSaleId = table.Column<int>(type: "integer", nullable: true),
                    OriginPointOfSaleSyncId = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockTransferLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockTransferId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    MenuItemId = table.Column<int>(type: "integer", nullable: true),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SentQuantity = table.Column<int>(type: "integer", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "CompanyAddress", "CompanyEmail", "CompanyIdNat", "CompanyNIF", "CompanyName", "CompanyPhone", "CompanyRCCM", "CurrentExchangeRate", "CurrentExchangeRateCNY", "CurrentExchangeRateEUR", "DefaultCurrency", "DefaultPriceMode", "DiscountBeforeTax", "ExchangeRateMode", "ExchangeRateUpdatedAt", "UpdatedAt" },
                values: new object[] { 1, "", "", "", "", "", "", "", 2800m, 385m, 3100m, "CDF", "TTC", true, "Manual", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)) });

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
                filter: "\"Type\" = 6");

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
                name: "IX_MenuItems_MenuId",
                table: "MenuItems",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_Name",
                table: "MenuItems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_PrinterProfileId",
                table: "MenuItems",
                column: "PrinterProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ProductId",
                table: "MenuItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_PrinterProfileId",
                table: "Menus",
                column: "PrinterProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_RestaurantId",
                table: "Menus",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_RestaurantId_Name",
                table: "Menus",
                columns: new[] { "RestaurantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_MenuItemId",
                table: "OrderItems",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId",
                table: "Orders",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TableId",
                table: "Orders",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_OrderId",
                table: "PaymentTransactions",
                column: "OrderId");

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
                name: "IX_PrinterProfiles_IsDefaultKitchen",
                table: "PrinterProfiles",
                column: "IsDefaultKitchen");

            migrationBuilder.CreateIndex(
                name: "IX_PrinterProfiles_IsDefaultReceipt",
                table: "PrinterProfiles",
                column: "IsDefaultReceipt");

            migrationBuilder.CreateIndex(
                name: "IX_PrinterProfiles_Name",
                table: "PrinterProfiles",
                column: "Name");

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
                name: "IX_Restaurants_Name",
                table: "Restaurants",
                column: "Name");

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
                name: "IX_Tables_RestaurantId",
                table: "Tables",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_RestaurantId_Number",
                table: "Tables",
                columns: new[] { "RestaurantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_Status",
                table: "Tables",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerBarcodeHash",
                table: "Users",
                column: "ManagerBarcodeHash");

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
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

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
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "DailyReports");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Menus");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Tables");

            migrationBuilder.DropTable(
                name: "PointsOfSale");

            migrationBuilder.DropTable(
                name: "PrinterProfiles");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "Restaurants");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
