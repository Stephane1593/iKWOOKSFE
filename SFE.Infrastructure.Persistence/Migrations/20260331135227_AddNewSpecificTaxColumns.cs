using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNewSpecificTaxColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TaxSpecificValue",
                table: "Products",
                newName: "SpecificTaxValue");

            migrationBuilder.AddColumn<int>(
                name: "SpecificTaxType",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalFixedSpecificTax",
                table: "Invoices",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPercentSpecificTax",
                table: "Invoices",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SpecificTaxType",
                table: "InvoiceLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecificTaxValue",
                table: "InvoiceLines",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecificTaxType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TotalFixedSpecificTax",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TotalPercentSpecificTax",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SpecificTaxType",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "SpecificTaxValue",
                table: "InvoiceLines");

            migrationBuilder.RenameColumn(
                name: "SpecificTaxValue",
                table: "Products",
                newName: "TaxSpecificValue");
        }
    }
}
