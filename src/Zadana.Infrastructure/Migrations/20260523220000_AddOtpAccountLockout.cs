using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

/// <summary>
/// Adds account-level OTP lockout columns:
///   - AspNetUsers.OtpLockoutCount    (consecutive exhausted OTPs)
///   - AspNetUsers.OtpLockedUntilUtc  (block GenerateOtp/VerifyOtp until)
///
/// After three back-to-back exhaustions (5 wrong tries each) the account is
/// locked for 60 minutes against further OTP issuance, frustrating
/// brute-force attempts that hop between freshly issued codes.
/// </summary>
public partial class AddOtpAccountLockout : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "OtpLockoutCount",
            table: "AspNetUsers",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "OtpLockedUntilUtc",
            table: "AspNetUsers",
            type: "datetime2",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OtpLockoutCount", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "OtpLockedUntilUtc", table: "AspNetUsers");
    }
}
