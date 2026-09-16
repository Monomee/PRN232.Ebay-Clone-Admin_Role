using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixDecimalPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_OrderTable_OrderId",
                table: "FinancialTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_WithdrawalRequests_WithdrawalId",
                table: "FinancialTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlatformFees_Category_CategoryId",
                table: "PlatformFees");

            migrationBuilder.DropForeignKey(
                name: "FK_SellerWallets_User_SellerId",
                table: "SellerWallets");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_User_ProcessedBy",
                table: "WithdrawalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_User_SellerId",
                table: "WithdrawalRequests");

            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.columns WHERE Name = 'ReportReason' AND Object_ID = OBJECT_ID('Product')) BEGIN ALTER TABLE [Product] DROP COLUMN [ReportReason]; END");
            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.columns WHERE Name = 'ReportedBy' AND Object_ID = OBJECT_ID('Product')) BEGIN ALTER TABLE [Product] DROP COLUMN [ReportedBy]; END");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                table: "WithdrawalRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WithdrawalRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "RejectionReason",
                table: "WithdrawalRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankName",
                table: "WithdrawalRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankAccountNumber",
                table: "WithdrawalRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankAccountName",
                table: "WithdrawalRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_OrderTable_OrderId",
                table: "FinancialTransactions",
                column: "OrderId",
                principalTable: "OrderTable",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_WithdrawalRequests_WithdrawalId",
                table: "FinancialTransactions",
                column: "WithdrawalId",
                principalTable: "WithdrawalRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformFees_Category_CategoryId",
                table: "PlatformFees",
                column: "CategoryId",
                principalTable: "Category",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SellerWallets_User_SellerId",
                table: "SellerWallets",
                column: "SellerId",
                principalTable: "User",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_User_ProcessedBy",
                table: "WithdrawalRequests",
                column: "ProcessedBy",
                principalTable: "User",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_User_SellerId",
                table: "WithdrawalRequests",
                column: "SellerId",
                principalTable: "User",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_OrderTable_OrderId",
                table: "FinancialTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialTransactions_WithdrawalRequests_WithdrawalId",
                table: "FinancialTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PlatformFees_Category_CategoryId",
                table: "PlatformFees");

            migrationBuilder.DropForeignKey(
                name: "FK_SellerWallets_User_SellerId",
                table: "SellerWallets");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_User_ProcessedBy",
                table: "WithdrawalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_WithdrawalRequests_User_SellerId",
                table: "WithdrawalRequests");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                table: "WithdrawalRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "WithdrawalRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "RejectionReason",
                table: "WithdrawalRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankName",
                table: "WithdrawalRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankAccountNumber",
                table: "WithdrawalRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BankAccountName",
                table: "WithdrawalRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ReportReason' AND Object_ID = OBJECT_ID('Product')) BEGIN ALTER TABLE [Product] ADD [ReportReason] nvarchar(max) NULL; END");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ReportedBy' AND Object_ID = OBJECT_ID('Product')) BEGIN ALTER TABLE [Product] ADD [ReportedBy] nvarchar(max) NULL; END");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_OrderTable_OrderId",
                table: "FinancialTransactions",
                column: "OrderId",
                principalTable: "OrderTable",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialTransactions_WithdrawalRequests_WithdrawalId",
                table: "FinancialTransactions",
                column: "WithdrawalId",
                principalTable: "WithdrawalRequests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformFees_Category_CategoryId",
                table: "PlatformFees",
                column: "CategoryId",
                principalTable: "Category",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_SellerWallets_User_SellerId",
                table: "SellerWallets",
                column: "SellerId",
                principalTable: "User",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_User_ProcessedBy",
                table: "WithdrawalRequests",
                column: "ProcessedBy",
                principalTable: "User",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_WithdrawalRequests_User_SellerId",
                table: "WithdrawalRequests",
                column: "SellerId",
                principalTable: "User",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
