using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePaymentSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MobileOperator",
                table: "InvoicePayments",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MobileOperator",
                table: "InvoicePayments");
        }
    }
}
