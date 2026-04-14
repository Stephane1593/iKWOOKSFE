using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvanceInvoiceSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdvanceGroupId",
                table: "Invoices",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentInvoiceId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingBalance",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAdvancesPaid",
                table: "Invoices",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_AdvanceGroupId",
                table: "Invoices",
                column: "AdvanceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ParentInvoiceId",
                table: "Invoices",
                column: "ParentInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Invoices_ParentInvoiceId",
                table: "Invoices",
                column: "ParentInvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Invoices_ParentInvoiceId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_AdvanceGroupId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ParentInvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "AdvanceGroupId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ParentInvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RemainingBalance",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TotalAdvancesPaid",
                table: "Invoices");
        }
    }
}
