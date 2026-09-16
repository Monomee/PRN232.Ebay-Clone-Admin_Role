using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerReviewFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReportedBySeller",
                table: "Review",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SellerReply",
                table: "Review",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SellerReplyCreatedAt",
                table: "Review",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerReportReason",
                table: "Review",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportedBySeller",
                table: "Review");

            migrationBuilder.DropColumn(
                name: "SellerReply",
                table: "Review");

            migrationBuilder.DropColumn(
                name: "SellerReplyCreatedAt",
                table: "Review");

            migrationBuilder.DropColumn(
                name: "SellerReportReason",
                table: "Review");
        }
    }
}
