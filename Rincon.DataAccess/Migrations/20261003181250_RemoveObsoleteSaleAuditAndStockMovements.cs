using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rincon.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteSaleAuditAndStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectSales_AspNetUsers_VoidedByUserId",
                table: "DirectSales");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_DirectSales_VoidedByUserId",
                table: "DirectSales");

            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "DirectSales");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "DirectSales");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "DirectSales");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "DirectSales");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "DirectSales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "DirectSales",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "DirectSales",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedByUserId",
                table: "DirectSales",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PreviousQuantity = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectSales_VoidedByUserId",
                table: "DirectSales",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId_Date",
                table: "StockMovements",
                columns: new[] { "ProductId", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_DirectSales_AspNetUsers_VoidedByUserId",
                table: "DirectSales",
                column: "VoidedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
