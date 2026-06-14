using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowBranchScopedVendorProductInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorProduct_Vendor_Master",
                table: "VendorProduct");

            migrationBuilder.Sql("""
                WITH CanonicalPricing AS (
                    SELECT
                        [Id],
                        [VendorId],
                        [MasterProductId],
                        [SellingPrice],
                        [CompareAtPrice],
                        [CostPrice],
                        [TradePrice],
                        ROW_NUMBER() OVER (
                            PARTITION BY [VendorId], [MasterProductId]
                            ORDER BY
                                CASE WHEN [VendorBranchId] IS NULL THEN 0 ELSE 1 END,
                                [CreatedAtUtc],
                                [Id]
                        ) AS [RowNumber]
                    FROM [VendorProduct]
                ),
                ChosenPricing AS (
                    SELECT
                        [VendorId],
                        [MasterProductId],
                        [SellingPrice],
                        [CompareAtPrice],
                        [CostPrice],
                        [TradePrice]
                    FROM CanonicalPricing
                    WHERE [RowNumber] = 1
                )
                UPDATE target
                SET
                    target.[SellingPrice] = source.[SellingPrice],
                    target.[CompareAtPrice] = source.[CompareAtPrice],
                    target.[CostPrice] = source.[CostPrice],
                    target.[TradePrice] = source.[TradePrice]
                FROM [VendorProduct] target
                INNER JOIN ChosenPricing source
                    ON source.[VendorId] = target.[VendorId]
                    AND source.[MasterProductId] = target.[MasterProductId];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_VendorProduct_Vendor_Master_Branch",
                table: "VendorProduct",
                columns: new[] { "VendorId", "MasterProductId", "VendorBranchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorProduct_Vendor_Master_Branch",
                table: "VendorProduct");

            migrationBuilder.CreateIndex(
                name: "IX_VendorProduct_Vendor_Master",
                table: "VendorProduct",
                columns: new[] { "VendorId", "MasterProductId" },
                unique: true);
        }
    }
}
