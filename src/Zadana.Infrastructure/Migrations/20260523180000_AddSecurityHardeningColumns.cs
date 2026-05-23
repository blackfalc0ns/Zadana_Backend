using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

/// <summary>
/// Adds the columns needed by the security hardening pass:
/// - RefreshToken.TokenHash      (SHA-256 hex of issued token)
/// - RefreshToken.WasReused      (reuse-detection flag)
/// - AspNetUsers.OtpAttempts     (OTP brute-force counter)
/// - AspNetUsers.PasswordResetOtpAttempts (password reset OTP counter)
///
/// Existing rows keep working: legacy plaintext tokens still match through
/// the repository's fallback path until they are rotated. The Token column
/// becomes nullable and its unique index is filtered to ignore nulls, while
/// a parallel filtered unique index is added on TokenHash.
///
/// Notes about table / column naming:
/// - The current model snapshot maps RefreshToken -> "RefreshToken" (singular)
///   with index "IX_RefreshToken_Token" and Token max length 512. We keep
///   those names so the migration applies cleanly on existing databases.
/// - For OtpCode / PasswordResetOtp the previous schema uses nvarchar(max);
///   we shrink to nvarchar(128) (room for SHA-256 hex + future algorithms).
/// </summary>
public partial class AddSecurityHardeningColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RefreshToken_Token",
            table: "RefreshToken");

        migrationBuilder.AlterColumn<string>(
            name: "Token",
            table: "RefreshToken",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(512)",
            oldMaxLength: 512);

        migrationBuilder.AddColumn<string>(
            name: "TokenHash",
            table: "RefreshToken",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "WasReused",
            table: "RefreshToken",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshToken_Token",
            table: "RefreshToken",
            column: "Token",
            unique: true,
            filter: "[Token] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshToken_TokenHash",
            table: "RefreshToken",
            column: "TokenHash",
            unique: true,
            filter: "[TokenHash] IS NOT NULL");

        migrationBuilder.AlterColumn<string>(
            name: "OtpCode",
            table: "AspNetUsers",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "PasswordResetOtp",
            table: "AspNetUsers",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OtpAttempts",
            table: "AspNetUsers",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "PasswordResetOtpAttempts",
            table: "AspNetUsers",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RefreshToken_TokenHash",
            table: "RefreshToken");

        migrationBuilder.DropIndex(
            name: "IX_RefreshToken_Token",
            table: "RefreshToken");

        migrationBuilder.DropColumn(
            name: "TokenHash",
            table: "RefreshToken");

        migrationBuilder.DropColumn(
            name: "WasReused",
            table: "RefreshToken");

        migrationBuilder.DropColumn(
            name: "OtpAttempts",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "PasswordResetOtpAttempts",
            table: "AspNetUsers");

        migrationBuilder.AlterColumn<string>(
            name: "Token",
            table: "RefreshToken",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: string.Empty,
            oldClrType: typeof(string),
            oldType: "nvarchar(512)",
            oldMaxLength: 512,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "OtpCode",
            table: "AspNetUsers",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(128)",
            oldMaxLength: 128,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "PasswordResetOtp",
            table: "AspNetUsers",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(128)",
            oldMaxLength: 128,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshToken_Token",
            table: "RefreshToken",
            column: "Token",
            unique: true);
    }
}
