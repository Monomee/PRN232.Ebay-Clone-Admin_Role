using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerLevelAndEstimatedSettlementDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SellerEarnings",
                table: "OrderTable",
                newName: "sellerEarnings");

            migrationBuilder.RenameColumn(
                name: "PlatformFee",
                table: "OrderTable",
                newName: "platformFee");

            migrationBuilder.AddColumn<string>(
                name: "SellerLevel",
                table: "User",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "totalPrice",
                table: "OrderTable",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedSettlementDate",
                table: "OrderTable",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellerLevel",
                table: "User");

            migrationBuilder.DropColumn(
                name: "EstimatedSettlementDate",
                table: "OrderTable");

            migrationBuilder.RenameColumn(
                name: "sellerEarnings",
                table: "OrderTable",
                newName: "SellerEarnings");

            migrationBuilder.RenameColumn(
                name: "platformFee",
                table: "OrderTable",
                newName: "PlatformFee");

            migrationBuilder.AlterColumn<decimal>(
                name: "totalPrice",
                table: "OrderTable",
                type: "decimal(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);
        }
    }
}
