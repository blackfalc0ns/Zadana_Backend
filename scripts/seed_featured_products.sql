-- Featured products seed script
-- Usage:
-- Run the script as-is.
-- It will:
-- 1) Upsert the featured selection settings.
-- 2) Insert up to 3 manual featured vendor products automatically from eligible data.
-- 3) Skip duplicates if matching placements already exist.

-- ------------------------------------------------------------
-- Discover active master products
-- ------------------------------------------------------------
SELECT TOP (20)
    mp.Id AS MasterProductId,
    mp.NameAr,
    mp.NameEn,
    mp.Status,
    mp.CreatedAtUtc
FROM MasterProduct mp
WHERE mp.Status = 'Active'
ORDER BY mp.CreatedAtUtc DESC;

-- ------------------------------------------------------------
-- Discover active vendor products eligible for manual placement
-- ------------------------------------------------------------
SELECT TOP (20)
    vp.Id AS VendorProductId,
    vp.MasterProductId,
    vp.SellingPrice,
    vp.CompareAtPrice,
    vp.StockQuantity,
    vp.Status,
    vp.IsAvailable,
    v.BusinessNameAr,
    v.BusinessNameEn
FROM VendorProduct vp
INNER JOIN Vendor v ON v.Id = vp.VendorId
INNER JOIN MasterProduct mp ON mp.Id = vp.MasterProductId
WHERE vp.Status = 'Active'
  AND vp.IsAvailable = 1
  AND vp.StockQuantity > 0
  AND mp.Status = 'Active'
  AND v.Status = 'Active'
  AND v.AcceptOrders = 1
ORDER BY vp.CreatedAtUtc DESC;

-- ------------------------------------------------------------
-- Upsert featured auto-selection settings
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM FeaturedProductSelectionSettings)
BEGIN
    INSERT INTO FeaturedProductSelectionSettings
    (
        Id,
        SelectionMode,
        TargetCount,
        MinSalesCount,
        MinStoreCount,
        RequireDiscount,
        ExcludeProductsAlreadyInSpecialOffers,
        CreatedAtUtc,
        UpdatedAtUtc
    )
    VALUES
    (
        NEWID(),
        'ManualFirstAutoFill',
        10,
        1,
        2,
        0,
        1,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END
ELSE
BEGIN
    UPDATE FeaturedProductSelectionSettings
    SET
        SelectionMode = 'ManualFirstAutoFill',
        TargetCount = 10,
        MinSalesCount = 1,
        MinStoreCount = 2,
        RequireDiscount = 0,
        ExcludeProductsAlreadyInSpecialOffers = 1,
        UpdatedAtUtc = SYSUTCDATETIME();
END;

-- ------------------------------------------------------------
-- Optional cleanup: remove old manual placements
-- Uncomment if you want to reset the manual featured list first
-- ------------------------------------------------------------
-- DELETE FROM FeaturedProductPlacement;

-- ------------------------------------------------------------
-- Automatic manual featured placements
-- This inserts up to 3 eligible VendorProduct placements automatically.
-- Ranking:
--   1) discounted products first
--   2) higher stock
--   3) newest products
-- ------------------------------------------------------------
;WITH EligibleVendorProducts AS
(
    SELECT TOP (3)
        vp.Id AS VendorProductId,
        vp.MasterProductId,
        ROW_NUMBER() OVER
        (
            ORDER BY
                CASE
                    WHEN vp.CompareAtPrice IS NOT NULL AND vp.CompareAtPrice > vp.SellingPrice THEN 1
                    ELSE 0
                END DESC,
                vp.StockQuantity DESC,
                vp.CreatedAtUtc DESC
        ) AS DisplayOrder
    FROM VendorProduct vp
    INNER JOIN Vendor v ON v.Id = vp.VendorId
    INNER JOIN MasterProduct mp ON mp.Id = vp.MasterProductId
    WHERE vp.Status = 'Active'
      AND vp.IsAvailable = 1
      AND vp.StockQuantity > 0
      AND mp.Status = 'Active'
      AND v.Status = 'Active'
      AND v.AcceptOrders = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM FeaturedProductPlacement fpp
          WHERE fpp.PlacementType = 'VendorProduct'
            AND fpp.VendorProductId = vp.Id
      )
    ORDER BY
        CASE
            WHEN vp.CompareAtPrice IS NOT NULL AND vp.CompareAtPrice > vp.SellingPrice THEN 1
            ELSE 0
        END DESC,
        vp.StockQuantity DESC,
        vp.CreatedAtUtc DESC
)
INSERT INTO FeaturedProductPlacement
(
    Id,
    PlacementType,
    VendorProductId,
    MasterProductId,
    DisplayOrder,
    IsActive,
    StartsAtUtc,
    EndsAtUtc,
    Note,
    CreatedAtUtc,
    UpdatedAtUtc
)
SELECT
    NEWID(),
    'VendorProduct',
    eligible.VendorProductId,
    NULL,
    eligible.DisplayOrder,
    1,
    NULL,
    NULL,
    N'Auto-seeded featured vendor product',
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM EligibleVendorProducts eligible;

-- ------------------------------------------------------------
-- Verify current featured manual placements
-- ------------------------------------------------------------
SELECT
    fpp.Id,
    fpp.PlacementType,
    fpp.VendorProductId,
    fpp.MasterProductId,
    fpp.DisplayOrder,
    fpp.IsActive,
    fpp.StartsAtUtc,
    fpp.EndsAtUtc,
    fpp.Note,
    fpp.CreatedAtUtc,
    fpp.UpdatedAtUtc
FROM FeaturedProductPlacement fpp
ORDER BY fpp.DisplayOrder, fpp.CreatedAtUtc DESC;
