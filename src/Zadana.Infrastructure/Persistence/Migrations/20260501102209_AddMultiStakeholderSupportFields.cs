using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiStakeholderSupportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DriverRespondedAtUtc",
                table: "OrderSupportCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverResponse",
                table: "OrderSupportCases",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitiatorRole",
                table: "OrderSupportCases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "customer");

            migrationBuilder.AddColumn<string>(
                name: "ResolutionCode",
                table: "OrderSupportCases",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VendorRespondedAtUtc",
                table: "OrderSupportCases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorResponse",
                table: "OrderSupportCases",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverRespondedAtUtc",
                table: "OrderSupportCases");

            migrationBuilder.DropColumn(
                name: "DriverResponse",
                table: "OrderSupportCases");

            migrationBuilder.DropColumn(
                name: "InitiatorRole",
                table: "OrderSupportCases");

            migrationBuilder.DropColumn(
                name: "ResolutionCode",
                table: "OrderSupportCases");

            migrationBuilder.DropColumn(
                name: "VendorRespondedAtUtc",
                table: "OrderSupportCases");

            migrationBuilder.DropColumn(
                name: "VendorResponse",
                table: "OrderSupportCases");
        }
    }
}
