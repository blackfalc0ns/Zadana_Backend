SET NOCOUNT ON;

DECLARE @now datetime2 = SYSUTCDATETIME();

DECLARE @Units TABLE
(
    NameAr nvarchar(100) NOT NULL,
    NameEn nvarchar(100) NOT NULL,
    Symbol nvarchar(20) NULL
);

INSERT INTO @Units (NameAr, NameEn, Symbol)
VALUES
    (N'قطعة', N'Piece', N'pc'),
    (N'عبوة', N'Pack', N'pack'),
    (N'علبة', N'Box', N'box'),
    (N'كرتونة', N'Carton', N'ctn'),
    (N'صندوق', N'Case', N'case'),
    (N'زجاجة', N'Bottle', N'btl'),
    (N'برطمان', N'Jar', N'jar'),
    (N'علبة معدنية', N'Can', N'can'),
    (N'كيس تغليف', N'Pouch', N'pouch'),
    (N'كيس صغير', N'Sachet', N'sachet'),
    (N'كيس', N'Bag', N'bag'),
    (N'رول', N'Roll', N'roll'),
    (N'ورقة', N'Sheet', N'sheet'),
    (N'زوج', N'Pair', N'pair'),
    (N'طقم', N'Set', N'set'),
    (N'حزمة', N'Bundle', N'bundle'),
    (N'دستة', N'Dozen', N'dz'),
    (N'صينية', N'Tray', N'tray'),
    (N'قفص', N'Crate', N'crate'),
    (N'طبالية', N'Pallet', N'pallet'),
    (N'شريط', N'Strip', N'strip'),
    (N'شريط تغليف', N'Blister', N'blister'),
    (N'أنبوب', N'Tube', N'tube'),
    (N'قالب', N'Bar', N'bar'),
    (N'رغيف', N'Loaf', N'loaf'),
    (N'شريحة', N'Slice', N'slice'),
    (N'كبسولة', N'Capsule', N'cap'),
    (N'قرص', N'Tablet', N'tab'),
    (N'قارورة صغيرة', N'Vial', N'vial'),
    (N'أمبول', N'Ampoule', N'amp'),
    (N'كيلوجرام', N'Kilogram', N'kg'),
    (N'جرام', N'Gram', N'g'),
    (N'ملليجرام', N'Milligram', N'mg'),
    (N'لتر', N'Liter', N'L'),
    (N'ملليلتر', N'Milliliter', N'mL'),
    (N'متر', N'Meter', N'm'),
    (N'سنتيمتر', N'Centimeter', N'cm'),
    (N'ملليمتر', N'Millimeter', N'mm'),
    (N'متر مربع', N'Square Meter', N'm2'),
    (N'متر مكعب', N'Cubic Meter', N'm3'),
    (N'ساعة', N'Hour', N'hr'),
    (N'يوم', N'Day', N'day'),
    (N'خدمة', N'Service', N'svc'),
    (N'زيارة', N'Visit', N'visit'),
    (N'وجبة', N'Meal', N'meal'),
    (N'حصة تقديم', N'Serving', N'serving'),
    (N'حصة', N'Portion', N'portion'),
    (N'كوب', N'Cup', N'cup'),
    (N'مغرفة', N'Scoop', N'scoop'),
    (N'قطرة', N'Drop', N'drop');

MERGE dbo.UnitOfMeasure AS target
USING @Units AS source
    ON target.NameEn = source.NameEn
WHEN MATCHED THEN
    UPDATE SET
        NameAr = source.NameAr,
        Symbol = source.Symbol,
        IsActive = CAST(1 AS bit),
        UpdatedAtUtc = @now
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, NameAr, NameEn, Symbol, IsActive, CreatedAtUtc, UpdatedAtUtc)
    VALUES (NEWID(), source.NameAr, source.NameEn, source.Symbol, CAST(1 AS bit), @now, @now);

SELECT COUNT(*) AS SeededUnitsCount
FROM @Units;
