using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxGroupAType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TaxGroupAType",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxGroupAType",
                table: "InvoiceLines",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxGroupAType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TaxGroupAType",
                table: "InvoiceLines");
        }
    }
}
