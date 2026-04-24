using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateUserPosAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedPosIds",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "PointOfSaleId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PointOfSaleId",
                table: "Users",
                column: "PointOfSaleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_PointsOfSale_PointOfSaleId",
                table: "Users",
                column: "PointOfSaleId",
                principalTable: "PointsOfSale",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_PointsOfSale_PointOfSaleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PointOfSaleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PointOfSaleId",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "AssignedPosIds",
                table: "Users",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "[]");
        }
    }
}
