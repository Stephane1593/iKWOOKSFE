using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCardTerminalFieldsToInvoicePayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthCode",
                table: "InvoicePayments",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardScheme",
                table: "InvoicePayments",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaskedPan",
                table: "InvoicePayments",
                type: "TEXT",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rrn",
                table: "InvoicePayments",
                type: "TEXT",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminalId",
                table: "InvoicePayments",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionRef",
                table: "InvoicePayments",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthCode",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "CardScheme",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "MaskedPan",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "Rrn",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "InvoicePayments");

            migrationBuilder.DropColumn(
                name: "TransactionRef",
                table: "InvoicePayments");
        }
    }
}
