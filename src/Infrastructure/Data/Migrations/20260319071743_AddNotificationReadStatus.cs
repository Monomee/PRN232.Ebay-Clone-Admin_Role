using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationReadStatus : Migration
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

            migrationBuilder.AlterColumn<decimal>(
                name: "totalPrice",
                table: "OrderTable",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserNotificationReads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotificationReads_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserNotificationReads_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationReads_NotificationId",
                table: "UserNotificationReads",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationReads_UserId",
                table: "UserNotificationReads",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserNotificationReads");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Notifications");

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
