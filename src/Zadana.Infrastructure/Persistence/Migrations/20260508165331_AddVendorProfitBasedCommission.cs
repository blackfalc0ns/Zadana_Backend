using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorProfitBasedCommission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "VendorProductBulkOperationItem",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TradePrice",
                table: "VendorProductBulkOperationItem",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                table: "VendorProduct",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TradePrice",
                table: "VendorProduct",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TradeUnitPrice",
                table: "OrderItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VendorProfitPerUnit",
                table: "OrderItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "VendorProductBulkOperationItem");

            migrationBuilder.DropColumn(
                name: "TradePrice",
                table: "VendorProductBulkOperationItem");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "VendorProduct");

            migrationBuilder.DropColumn(
                name: "TradePrice",
                table: "VendorProduct");

            migrationBuilder.DropColumn(
                name: "TradeUnitPrice",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "VendorProfitPerUnit",
                table: "OrderItems");
        }
    }
}
