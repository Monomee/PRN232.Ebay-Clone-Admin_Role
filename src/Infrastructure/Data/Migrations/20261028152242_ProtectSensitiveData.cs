using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EbayClone.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProtectSensitiveData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "CccdLookupHash",
                table: "User",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // Adopt the table created by the previous seeder without deleting its records.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.SensitiveDataLogs', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[SensitiveDataLogs] (
                        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SensitiveDataLogs] PRIMARY KEY,
                        [ActorId] int NOT NULL,
                        [ActorUsername] nvarchar(256) NULL,
                        [ActorRole] nvarchar(100) NULL,
                        [Action] nvarchar(100) NOT NULL,
                        [TargetEntity] nvarchar(100) NOT NULL,
                        [TargetId] nvarchar(100) NULL,
                        [DataClassification] nvarchar(50) NOT NULL,
                        [EncryptedData] nvarchar(max) NOT NULL,
                        [EncryptionAlgorithm] nvarchar(50) NOT NULL,
                        [KeyIdentifier] nvarchar(50) NOT NULL,
                        [DataHash] nvarchar(256) NULL,
                        [IpAddress] nvarchar(50) NULL,
                        [UserAgent] nvarchar(500) NULL,
                        [CreatedAt] datetimeoffset NOT NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [LastModified] datetimeoffset NOT NULL,
                        [LastModifiedBy] nvarchar(max) NULL
                    );
                END
                ELSE
                BEGIN
                    IF COL_LENGTH(N'dbo.SensitiveDataLogs', N'Id') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'ActorId') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'ActorUsername') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'ActorRole') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'Action') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'TargetEntity') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'TargetId') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'DataClassification') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'EncryptedData') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'EncryptionAlgorithm') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'KeyIdentifier') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'DataHash') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'IpAddress') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'UserAgent') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'CreatedAt') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'CreatedBy') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'LastModified') IS NULL OR
                       COL_LENGTH(N'dbo.SensitiveDataLogs', N'LastModifiedBy') IS NULL
                        THROW 51000, 'Unexpected SensitiveDataLogs schema. Review it before migration; no data was removed.', 1;
                    UPDATE [dbo].[SensitiveDataLogs]
                        SET [LastModified] = COALESCE([LastModified], [CreatedAt]);
                    ALTER TABLE [dbo].[SensitiveDataLogs] ALTER COLUMN [LastModified] datetimeoffset NOT NULL;
                    ALTER TABLE [dbo].[SensitiveDataLogs] ALTER COLUMN [ActorRole] nvarchar(100) NULL;
                    ALTER TABLE [dbo].[SensitiveDataLogs] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;
                    ALTER TABLE [dbo].[SensitiveDataLogs] ALTER COLUMN [LastModifiedBy] nvarchar(max) NULL;
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_User_CccdLookupHash",
                table: "User",
                column: "CccdLookupHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Sensitive-data cutover cannot be reversed by dropping audit data or narrowing ciphertext columns. Restore the coordinated DB/application backup instead.");
        }
    }
}
