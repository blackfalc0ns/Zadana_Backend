using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges_May25 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[RefreshToken]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[RefreshTokens]', N'U') IS NULL
                BEGIN
                    IF OBJECT_ID(N'[dbo].[FK_RefreshToken_AspNetUsers_UserId]', N'F') IS NOT NULL
                        ALTER TABLE [dbo].[RefreshToken] DROP CONSTRAINT [FK_RefreshToken_AspNetUsers_UserId];

                    IF OBJECT_ID(N'[dbo].[PK_RefreshToken]', N'PK') IS NOT NULL
                        ALTER TABLE [dbo].[RefreshToken] DROP CONSTRAINT [PK_RefreshToken];

                    EXEC sp_rename N'[dbo].[RefreshToken]', N'RefreshTokens';

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshToken_UserId' AND object_id = OBJECT_ID(N'[dbo].[RefreshTokens]'))
                        EXEC sp_rename N'[dbo].[RefreshTokens].[IX_RefreshToken_UserId]', N'IX_RefreshTokens_UserId', N'INDEX';
                END

                IF OBJECT_ID(N'[dbo].[RefreshTokens]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[RefreshTokens]', N'Token') IS NOT NULL
                        ALTER TABLE [dbo].[RefreshTokens] ALTER COLUMN [Token] nvarchar(512) NULL;

                    IF COL_LENGTH(N'[dbo].[RefreshTokens]', N'IsRevoked') IS NOT NULL
                    BEGIN
                        UPDATE [dbo].[RefreshTokens] SET [IsRevoked] = 0 WHERE [IsRevoked] IS NULL;
                        ALTER TABLE [dbo].[RefreshTokens] ALTER COLUMN [IsRevoked] bit NOT NULL;
                    END

                    IF OBJECT_ID(N'[dbo].[PK_RefreshTokens]', N'PK') IS NULL AND COL_LENGTH(N'[dbo].[RefreshTokens]', N'Id') IS NOT NULL
                        ALTER TABLE [dbo].[RefreshTokens] ADD CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]);

                    IF OBJECT_ID(N'[dbo].[FK_RefreshTokens_AspNetUsers_UserId]', N'F') IS NULL
                       AND COL_LENGTH(N'[dbo].[RefreshTokens]', N'UserId') IS NOT NULL
                       AND OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
                        ALTER TABLE [dbo].[RefreshTokens] ADD CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId]
                            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_AspNetUsers_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RefreshTokens",
                table: "RefreshTokens");

            migrationBuilder.RenameTable(
                name: "RefreshTokens",
                newName: "RefreshToken");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshToken",
                newName: "IX_RefreshToken_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshToken",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsRevoked",
                table: "RefreshToken",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RefreshToken",
                table: "RefreshToken",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshToken_AspNetUsers_UserId",
                table: "RefreshToken",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
