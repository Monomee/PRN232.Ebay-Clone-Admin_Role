using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUserIdentifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two replicas syncing at once (before the job lock) may have written the same identifier twice.
            // Identifier rows are derived data (rebuilt every 10 minutes), so keeping one copy loses nothing.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY UserId, Type, ValueHash ORDER BY Id) AS n FROM UserIdentifiers
)
DELETE FROM ranked WHERE n > 1;");

            migrationBuilder.DropIndex(
                name: "IX_UserIdentifiers_UserId",
                table: "UserIdentifiers");

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentifiers_UserId_Type_ValueHash",
                table: "UserIdentifiers",
                columns: new[] { "UserId", "Type", "ValueHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserIdentifiers_UserId_Type_ValueHash",
                table: "UserIdentifiers");

            migrationBuilder.CreateIndex(
                name: "IX_UserIdentifiers_UserId",
                table: "UserIdentifiers",
                column: "UserId");
        }
    }
}
