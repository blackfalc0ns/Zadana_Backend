using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantUserAddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordResetOtp",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetOtpExpiry",
                table: "User",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetOtp",
                table: "User");

            migrationBuilder.DropColumn(
                name: "PasswordResetOtpExpiry",
                table: "User");
        }
    }
}
