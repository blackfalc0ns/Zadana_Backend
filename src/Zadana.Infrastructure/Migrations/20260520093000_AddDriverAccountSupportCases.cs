using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260520093000_AddDriverAccountSupportCases")]
public partial class AddDriverAccountSupportCases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrderSupportCases_Orders_OrderId",
            table: "OrderSupportCases");

        migrationBuilder.DropIndex(
            name: "IX_OrderSupportCases_OrderId_Status",
            table: "OrderSupportCases");

        migrationBuilder.AlterColumn<Guid>(
            name: "OrderId",
            table: "OrderSupportCases",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.AddColumn<Guid>(
            name: "DriverId",
            table: "OrderSupportCases",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrderSupportCases_DriverId_Type_Status",
            table: "OrderSupportCases",
            columns: new[] { "DriverId", "Type", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_OrderSupportCases_OrderId_Status",
            table: "OrderSupportCases",
            columns: new[] { "OrderId", "Status" });

        migrationBuilder.AddForeignKey(
            name: "FK_OrderSupportCases_Orders_OrderId",
            table: "OrderSupportCases",
            column: "OrderId",
            principalTable: "Orders",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrderSupportCases_Orders_OrderId",
            table: "OrderSupportCases");

        migrationBuilder.DropIndex(
            name: "IX_OrderSupportCases_DriverId_Type_Status",
            table: "OrderSupportCases");

        migrationBuilder.DropIndex(
            name: "IX_OrderSupportCases_OrderId_Status",
            table: "OrderSupportCases");

        migrationBuilder.Sql("DELETE FROM [OrderSupportCases] WHERE [OrderId] IS NULL");

        migrationBuilder.DropColumn(
            name: "DriverId",
            table: "OrderSupportCases");

        migrationBuilder.AlterColumn<Guid>(
            name: "OrderId",
            table: "OrderSupportCases",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrderSupportCases_OrderId_Status",
            table: "OrderSupportCases",
            columns: new[] { "OrderId", "Status" });

        migrationBuilder.AddForeignKey(
            name: "FK_OrderSupportCases_Orders_OrderId",
            table: "OrderSupportCases",
            column: "OrderId",
            principalTable: "Orders",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
