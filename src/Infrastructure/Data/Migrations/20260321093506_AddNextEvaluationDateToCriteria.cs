using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNextEvaluationDateToCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AboveStandardMaxLateRate",
                table: "SellerLevelCriteria",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextEvaluationDate",
                table: "SellerLevelCriteria",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AboveStandardMaxLateRate",
                table: "SellerLevelCriteria");

            migrationBuilder.DropColumn(
                name: "NextEvaluationDate",
                table: "SellerLevelCriteria");
        }
    }
}
