using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerLevelCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_WithdrawalRequests_WithdrawalId",
                table: "FinancialTransactions");

            migrationBuilder.DropTable(
                name: "WithdrawalRequests");

            migrationBuilder.DropIndex(
                name: "IX_FinancialTransactions_WithdrawalId",
                table: "FinancialTransactions");

            migrationBuilder.DropColumn(
                name: "WithdrawalId",
                table: "FinancialTransactions");

            migrationBuilder.CreateTable(
                name: "SellerLevelCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopRatedMinTransactions = table.Column<int>(type: "int", nullable: false),
                    TopRatedMinSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TopRatedMinDays = table.Column<int>(type: "int", nullable: false),
                    TopRatedMaxUnresolvedCases = table.Column<int>(type: "int", nullable: false),
                    TopRatedMaxDefectRate = table.Column<double>(type: "float", nullable: false),
                    TopRatedMaxLateRate = table.Column<double>(type: "float", nullable: false),
                    AboveStandardMaxDefectRate = table.Column<double>(type: "float", nullable: false),
                    AboveStandardMaxUnresolvedCases = table.Column<int>(type: "int", nullable: false),
                    AboveStandardMaxUnresolvedRate = table.Column<double>(type: "float", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerLevelCriteria", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SellerLevelCriteria");

            migrationBuilder.AddColumn<int>(
                name: "WithdrawalId",
                table: "FinancialTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessedBy = table.Column<int>(type: "int", nullable: true),
                    SellerId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BankAccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_User_ProcessedBy",
                        column: x => x.ProcessedBy,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_User_SellerId",
                        column: x => x.SellerId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransactions_WithdrawalId",
                table: "FinancialTransactions",
                column: "WithdrawalId");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_ProcessedBy",
                table: "WithdrawalRequests",
                column: "ProcessedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_SellerId",
                table: "WithdrawalRequests",
                column: "SellerId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_WithdrawalRequests_WithdrawalId",
                table: "FinancialTransactions",
                column: "WithdrawalId",
                principalTable: "WithdrawalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
