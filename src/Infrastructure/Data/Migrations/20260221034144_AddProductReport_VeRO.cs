using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductReport_VeRO : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Created",
                table: "ProductReports",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "Created",
                table: "Product",
                newName: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "ProductReports",
                newName: "Created");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Product",
                newName: "Created");
        }
    }
}
