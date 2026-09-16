using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MergeAdminRoleFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "FinancialTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_UserId",
                table: "FinancialTransactions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_User_UserId",
                table: "FinancialTransactions",
                column: "UserId",
                principalTable: "User",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_User_UserId",
                table: "FinancialTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_UserId",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "FinancialTransactions");
        }
    }
}
