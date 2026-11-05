using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutHoldUntil",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RiskCheckedAt",
                table: "User",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                table: "User",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserIdentifiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValueHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserIdentifiers_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentifiers_UserId",
                table: "UserIdentifiers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentifiers_ValueHash",
                table: "UserIdentifiers",
                column: "ValueHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserIdentifiers");

            migrationBuilder.DropColumn(
                name: "PayoutHoldUntil",
                table: "User");

            migrationBuilder.DropColumn(
                name: "RiskCheckedAt",
                table: "User");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "User");
        }
    }
}
