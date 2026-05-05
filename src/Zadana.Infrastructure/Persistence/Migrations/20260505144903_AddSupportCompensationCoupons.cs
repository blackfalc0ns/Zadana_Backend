using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportCompensationCoupons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompensationCouponId",
                table: "OrderSupportCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompensationType",
                table: "OrderSupportCases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedUserId",
                table: "Coupons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderSupportCaseId",
                table: "Coupons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "Coupons",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompensationCouponId",
                table: "OrderSupportCases");

            migrationBuilder.DropColumn(
                name: "CompensationType",
                table: "OrderSupportCases");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "OrderSupportCaseId",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Coupons");
        }
    }
}
