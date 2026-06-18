using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260614120000_AllowBranchScopedVendorProductInventory")]
    public partial class AllowBranchScopedVendorProductInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[VendorProduct]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[VendorProduct]')
                          AND [name] = N'IX_VendorProduct_Vendor_Master')
                    BEGIN
                        DROP INDEX [IX_VendorProduct_Vendor_Master] ON [dbo].[VendorProduct];
                    END;

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
                        FROM [dbo].[VendorProduct]
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
                    FROM [dbo].[VendorProduct] target
                    INNER JOIN ChosenPricing source
                        ON source.[VendorId] = target.[VendorId]
                        AND source.[MasterProductId] = target.[MasterProductId];

                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[VendorProduct]')
                          AND [name] = N'IX_VendorProduct_Vendor_Master_Branch')
                    BEGIN
                        CREATE UNIQUE INDEX [IX_VendorProduct_Vendor_Master_Branch]
                            ON [dbo].[VendorProduct] ([VendorId], [MasterProductId], [VendorBranchId]);
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[VendorProduct]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[VendorProduct]')
                          AND [name] = N'IX_VendorProduct_Vendor_Master_Branch')
                    BEGIN
                        DROP INDEX [IX_VendorProduct_Vendor_Master_Branch] ON [dbo].[VendorProduct];
                    END;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[VendorProduct]')
                          AND [name] = N'IX_VendorProduct_Vendor_Master')
                    BEGIN
                        CREATE UNIQUE INDEX [IX_VendorProduct_Vendor_Master]
                            ON [dbo].[VendorProduct] ([VendorId], [MasterProductId]);
                    END;
                END;
                """);
        }
    }
}
