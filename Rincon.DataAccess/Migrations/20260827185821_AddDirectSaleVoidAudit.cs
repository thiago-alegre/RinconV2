using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rincon.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectSaleVoidAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "DirectSales",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedByUserId",
                table: "DirectSales",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectSales_VoidedByUserId",
                table: "DirectSales",
                column: "VoidedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectSales_AspNetUsers_VoidedByUserId",
                table: "DirectSales",
                column: "VoidedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectSales_AspNetUsers_VoidedByUserId",
                table: "DirectSales");

            migrationBuilder.DropIndex(
                name: "IX_DirectSales_VoidedByUserId",
                table: "DirectSales");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "DirectSales");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "DirectSales");
        }
    }
}
