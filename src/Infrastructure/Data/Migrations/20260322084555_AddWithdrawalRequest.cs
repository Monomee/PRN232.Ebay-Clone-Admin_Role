using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    SellerId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankAccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedBy = table.Column<int>(type: "int", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
