using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rincon.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddLooseSaleItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectSaleItems_Products_ProductId",
                table: "DirectSaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_DirectSaleReturnItems_Products_ProductId",
                table: "DirectSaleReturnItems");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "DirectSaleReturnItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "DirectSaleItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectSaleItems_Products_ProductId",
                table: "DirectSaleItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectSaleReturnItems_Products_ProductId",
                table: "DirectSaleReturnItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "DirectSaleItems" WHERE "ProductId" IS NULL)
                       OR EXISTS (SELECT 1 FROM "DirectSaleReturnItems" WHERE "ProductId" IS NULL) THEN
                        RAISE EXCEPTION 'No se puede revertir AddLooseSaleItems mientras existan ventas de productos sueltos.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_DirectSaleItems_Products_ProductId",
                table: "DirectSaleItems");

            migrationBuilder.DropForeignKey(
                name: "FK_DirectSaleReturnItems_Products_ProductId",
                table: "DirectSaleReturnItems");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "DirectSaleReturnItems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "DirectSaleItems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectSaleItems_Products_ProductId",
                table: "DirectSaleItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectSaleReturnItems_Products_ProductId",
                table: "DirectSaleReturnItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
