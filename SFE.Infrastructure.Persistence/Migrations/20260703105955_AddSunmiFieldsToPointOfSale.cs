using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SFE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSunmiFieldsToPointOfSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SunmiEnabled",
                table: "PointsOfSale",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SunmiTerminalId",
                table: "PointsOfSale",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SunmiTerminalUrl",
                table: "PointsOfSale",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SunmiEnabled",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "SunmiTerminalId",
                table: "PointsOfSale");

            migrationBuilder.DropColumn(
                name: "SunmiTerminalUrl",
                table: "PointsOfSale");
        }
    }
}
