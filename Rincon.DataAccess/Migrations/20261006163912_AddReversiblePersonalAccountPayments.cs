using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rincon.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddReversiblePersonalAccountPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "PersonalAccountPayments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReplacesPaymentId",
                table: "PersonalAccountPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "PersonalAccountPayments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "PersonalAccountPayments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedByUserId",
                table: "PersonalAccountPayments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAccountPayments_ReplacesPaymentId",
                table: "PersonalAccountPayments",
                column: "ReplacesPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAccountPayments_VoidedByUserId",
                table: "PersonalAccountPayments",
                column: "VoidedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalAccountPayments_AspNetUsers_VoidedByUserId",
                table: "PersonalAccountPayments",
                column: "VoidedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalAccountPayments_PersonalAccountPayments_ReplacesPay~",
                table: "PersonalAccountPayments",
                column: "ReplacesPaymentId",
                principalTable: "PersonalAccountPayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PersonalAccountPayments_AspNetUsers_VoidedByUserId",
                table: "PersonalAccountPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonalAccountPayments_PersonalAccountPayments_ReplacesPay~",
                table: "PersonalAccountPayments");

            migrationBuilder.DropIndex(
                name: "IX_PersonalAccountPayments_ReplacesPaymentId",
                table: "PersonalAccountPayments");

            migrationBuilder.DropIndex(
                name: "IX_PersonalAccountPayments_VoidedByUserId",
                table: "PersonalAccountPayments");

            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "PersonalAccountPayments");

            migrationBuilder.DropColumn(
                name: "ReplacesPaymentId",
                table: "PersonalAccountPayments");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "PersonalAccountPayments");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "PersonalAccountPayments");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "PersonalAccountPayments");
        }
    }
}
