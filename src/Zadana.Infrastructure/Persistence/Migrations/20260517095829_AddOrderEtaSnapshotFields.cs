using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderEtaSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EtaCalculatedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtaCalculationMode",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtaConfidence",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtaExplanation",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EtaIsApproximate",
                table: "Orders",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EtaMaxMinutes",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EtaMinMinutes",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtaSource",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EtaCalculatedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EtaCalculationMode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EtaConfidence",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EtaExplanation",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EtaIsApproximate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EtaMaxMinutes",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EtaMinMinutes",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EtaSource",
                table: "Orders");
        }
    }
}
