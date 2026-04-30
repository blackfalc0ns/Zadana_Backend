using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260430113000_AddDriverLocationUpdateAccessControl")]
    public class AddDriverLocationUpdateAccessControl : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationUpdatesBlockReason",
                table: "Drivers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocationUpdatesBlockedAtUtc",
                table: "Drivers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationUpdatesBlockedByUserId",
                table: "Drivers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocationUpdatesBlocked",
                table: "Drivers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationUpdatesBlockReason",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "LocationUpdatesBlockedAtUtc",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "LocationUpdatesBlockedByUserId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "IsLocationUpdatesBlocked",
                table: "Drivers");
        }
    }
}
