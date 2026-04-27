using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDriverPrimaryZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Drivers_DeliveryZones_PrimaryZoneId",
                table: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_PrimaryZoneId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "PrimaryZoneId",
                table: "Drivers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryZoneId",
                table: "Drivers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_PrimaryZoneId",
                table: "Drivers",
                column: "PrimaryZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_Drivers_DeliveryZones_PrimaryZoneId",
                table: "Drivers",
                column: "PrimaryZoneId",
                principalTable: "DeliveryZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
