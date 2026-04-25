using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverArrivalStateTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivedAtCustomerAtUtc",
                table: "DeliveryAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivedAtVendorAtUtc",
                table: "DeliveryAssignments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivedAtCustomerAtUtc",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "ArrivedAtVendorAtUtc",
                table: "DeliveryAssignments");
        }
    }
}
