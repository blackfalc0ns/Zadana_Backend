using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupAndDeliveryOtpWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryOtpCode",
                table: "DeliveryAssignments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryOtpExpiresAtUtc",
                table: "DeliveryAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryOtpVerifiedAtUtc",
                table: "DeliveryAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryOtpVerifiedByDriverId",
                table: "DeliveryAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupOtpCode",
                table: "DeliveryAssignments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupOtpExpiresAtUtc",
                table: "DeliveryAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupOtpVerifiedAtUtc",
                table: "DeliveryAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PickupOtpVerifiedByDriverId",
                table: "DeliveryAssignments",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryOtpCode",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "DeliveryOtpExpiresAtUtc",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "DeliveryOtpVerifiedAtUtc",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "DeliveryOtpVerifiedByDriverId",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "PickupOtpCode",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "PickupOtpExpiresAtUtc",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "PickupOtpVerifiedAtUtc",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "PickupOtpVerifiedByDriverId",
                table: "DeliveryAssignments");
        }
    }
}
