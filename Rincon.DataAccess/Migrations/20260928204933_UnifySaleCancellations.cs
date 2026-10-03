using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rincon.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UnifySaleCancellations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectSales_DirectSaleReturns_SourceReturnId",
                table: "DirectSales");

            migrationBuilder.DropIndex(
                name: "IX_DirectSales_SourceReturnId",
                table: "DirectSales");

            migrationBuilder.DropColumn(
                name: "ExchangeGroup",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SourceReturnId",
                table: "DirectSales");

            migrationBuilder.DropColumn(
                name: "IsExchange",
                table: "DirectSaleReturns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExchangeGroup",
                table: "Products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceReturnId",
                table: "DirectSales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExchange",
                table: "DirectSaleReturns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_DirectSales_SourceReturnId",
                table: "DirectSales",
                column: "SourceReturnId");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectSales_DirectSaleReturns_SourceReturnId",
                table: "DirectSales",
                column: "SourceReturnId",
                principalTable: "DirectSaleReturns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
