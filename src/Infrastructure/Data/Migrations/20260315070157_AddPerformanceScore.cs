using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PerformanceScore",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRefundedByEbayFund",
                table: "ReturnRequest",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionAction",
                table: "ReturnRequest",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnLabelUrl",
                table: "ReturnRequest",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerformanceScore",
                table: "User");

            migrationBuilder.DropColumn(
                name: "IsRefundedByEbayFund",
                table: "ReturnRequest");

            migrationBuilder.DropColumn(
                name: "ResolutionAction",
                table: "ReturnRequest");

            migrationBuilder.DropColumn(
                name: "ReturnLabelUrl",
                table: "ReturnRequest");
        }
    }
}
