using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rincon.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectSaleReturnsAndReplacementSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceReturnId",
                table: "DirectSales",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DirectSaleReturns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectSaleId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundMethod = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectSaleReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectSaleReturns_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DirectSaleReturns_DirectSales_DirectSaleId",
                        column: x => x.DirectSaleId,
                        principalTable: "DirectSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DirectSaleReturnItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DirectSaleReturnId = table.Column<int>(type: "integer", nullable: false),
                    DirectSaleItemId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnsToStock = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectSaleReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectSaleReturnItems_DirectSaleItems_DirectSaleItemId",
                        column: x => x.DirectSaleItemId,
                        principalTable: "DirectSaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectSaleReturnItems_DirectSaleReturns_DirectSaleReturnId",
                        column: x => x.DirectSaleReturnId,
                        principalTable: "DirectSaleReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectSaleReturnItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectSales_SourceReturnId",
                table: "DirectSales",
                column: "SourceReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectSaleReturnItems_DirectSaleItemId",
                table: "DirectSaleReturnItems",
                column: "DirectSaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectSaleReturnItems_DirectSaleReturnId",
                table: "DirectSaleReturnItems",
                column: "DirectSaleReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectSaleReturnItems_ProductId",
                table: "DirectSaleReturnItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectSaleReturns_DirectSaleId",
                table: "DirectSaleReturns",
                column: "DirectSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectSaleReturns_UserId",
                table: "DirectSaleReturns",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectSales_DirectSaleReturns_SourceReturnId",
                table: "DirectSales",
                column: "SourceReturnId",
                principalTable: "DirectSaleReturns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectSales_DirectSaleReturns_SourceReturnId",
                table: "DirectSales");

            migrationBuilder.DropTable(
                name: "DirectSaleReturnItems");

            migrationBuilder.DropTable(
                name: "DirectSaleReturns");

            migrationBuilder.DropIndex(
                name: "IX_DirectSales_SourceReturnId",
                table: "DirectSales");

            migrationBuilder.DropColumn(
                name: "SourceReturnId",
                table: "DirectSales");
        }
    }
}
