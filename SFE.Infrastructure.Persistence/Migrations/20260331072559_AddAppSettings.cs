using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppSettings : Migration
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
                    RCCM = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
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
                    TotalHTBeforeDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalTVA = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalSpecificTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
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
                        name: "FK_Invoices_Invoices_OriginalInvoiceId",
                        column: x => x.OriginalInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
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
                    McfBaudRate = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 115200)
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
                    UnitPriceHT = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitPriceTTC = table.Column<decimal>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PriceModification = table.Column<decimal>(type: "TEXT", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DiscountType = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    HasSpecificTax = table.Column<bool>(type: "INTEGER", nullable: false),
                    SpecificTaxName = table.Column<string>(type: "TEXT", nullable: false),
                    SpecificTaxRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    TaxApplicationMode = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxSpecificValue = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TaxSpecificAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AmountHTBeforeDiscount = table.Column<decimal>(type: "TEXT", nullable: false),
                    AmountHT = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AmountTVA = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AmountTTC = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
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
                    TaxSpecificValue = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StockQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    MinStockLevel = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    TrackStock = table.Column<bool>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                    AssignedPosIds = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: "[]"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "CompanyAddress", "CompanyEmail", "CompanyIdNat", "CompanyNIF", "CompanyName", "CompanyPhone", "CompanyRCCM", "CurrentExchangeRate", "DefaultCurrency", "DefaultPriceMode", "DiscountBeforeTax", "ExchangeRateMode", "ExchangeRateUpdatedAt", "UpdatedAt" },
                values: new object[] { 1, "", "", "", "", "", "", "", 2800m, "CDF", "TTC", true, "Manual", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Name",
                table: "Clients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_NIF",
                table: "Clients",
                column: "NIF");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_InvoiceId",
                table: "InvoicePayments",
                column: "InvoiceId");

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
                name: "IX_Invoices_Status",
                table: "Invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Type",
                table: "Invoices",
                column: "Type");

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
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

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
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "InvoicePayments");

            migrationBuilder.DropTable(
                name: "LoyaltyTransactions");

            migrationBuilder.DropTable(
                name: "PointsOfSale");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "LoyaltyAccounts");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
