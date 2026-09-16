using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlatformFeeConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_User_UserId",
                table: "FinancialTransactions");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "FinancialTransactions",
                newName: "SellerId");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "FinancialTransactions",
                newName: "Date");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_UserId",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_SellerId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_User_SellerId",
                table: "FinancialTransactions",
                column: "SellerId",
                principalTable: "User",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_User_SellerId",
                table: "FinancialTransactions");

            migrationBuilder.RenameColumn(
                name: "SellerId",
                table: "FinancialTransactions",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "FinancialTransactions",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_FinancialTransactions_SellerId",
                table: "FinancialTransactions",
                newName: "IX_FinancialTransactions_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_User_UserId",
                table: "FinancialTransactions",
                column: "UserId",
                principalTable: "User",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
