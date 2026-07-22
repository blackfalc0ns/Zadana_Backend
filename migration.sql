IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Brand] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [LogoUrl] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Brand] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Category] (
        [Id] uniqueidentifier NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [ParentCategoryId] uniqueidentifier NULL,
        [DisplayOrder] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Category] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Category_Category_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Coupons] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [DiscountType] nvarchar(50) NOT NULL,
        [DiscountValue] decimal(18,2) NOT NULL,
        [MinOrderAmount] decimal(18,2) NULL,
        [MaxDiscountAmount] decimal(18,2) NULL,
        [StartsAtUtc] datetime2 NULL,
        [EndsAtUtc] datetime2 NULL,
        [UsageLimit] int NULL,
        [PerUserLimit] int NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Coupons] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [ImageBank] (
        [Id] uniqueidentifier NOT NULL,
        [Url] nvarchar(1000) NOT NULL,
        [AltText] nvarchar(200) NULL,
        [Tags] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ImageBank] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [UnitOfMeasure] (
        [Id] uniqueidentifier NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UnitOfMeasure] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [User] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Phone] nvarchar(20) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [AccountStatus] nvarchar(20) NOT NULL,
        [IsEmailVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsPhoneVerified] bit NOT NULL DEFAULT CAST(0 AS bit),
        [LastLoginAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_User] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Wallet] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerType] nvarchar(20) NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [CurrentBalance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [PendingBalance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Wallet] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [MasterProduct] (
        [Id] uniqueidentifier NOT NULL,
        [NameAr] nvarchar(300) NOT NULL,
        [NameEn] nvarchar(300) NOT NULL,
        [DescriptionAr] nvarchar(2000) NULL,
        [DescriptionEn] nvarchar(2000) NULL,
        [Barcode] nvarchar(50) NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [BrandId] uniqueidentifier NULL,
        [UnitOfMeasureId] uniqueidentifier NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_MasterProduct] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MasterProduct_Brand_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brand] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MasterProduct_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MasterProduct_UnitOfMeasure_UnitOfMeasureId] FOREIGN KEY ([UnitOfMeasureId]) REFERENCES [UnitOfMeasure] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [CustomerAddresses] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Label] nvarchar(100) NULL,
        [ContactName] nvarchar(200) NOT NULL,
        [ContactPhone] nvarchar(50) NOT NULL,
        [AddressLine] nvarchar(500) NOT NULL,
        [BuildingNo] nvarchar(50) NULL,
        [FloorNo] nvarchar(50) NULL,
        [ApartmentNo] nvarchar(50) NULL,
        [City] nvarchar(100) NULL,
        [Area] nvarchar(100) NULL,
        [Latitude] decimal(10,7) NULL,
        [Longitude] decimal(10,7) NULL,
        [IsDefault] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerAddresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerAddresses_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Drivers] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [VehicleType] nvarchar(100) NULL,
        [NationalId] nvarchar(100) NULL,
        [LicenseNumber] nvarchar(100) NULL,
        [Status] nvarchar(50) NOT NULL,
        [IsAvailable] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Drivers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Drivers_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(1000) NOT NULL,
        [Type] nvarchar(100) NULL,
        [IsRead] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [RefreshToken] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] nvarchar(512) NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RevokedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RefreshToken] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshToken_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Vendor] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [BusinessNameAr] nvarchar(200) NOT NULL,
        [BusinessNameEn] nvarchar(200) NOT NULL,
        [BusinessType] nvarchar(50) NOT NULL,
        [CommercialRegistrationNumber] nvarchar(50) NOT NULL,
        [TaxId] nvarchar(50) NULL,
        [ContactEmail] nvarchar(256) NOT NULL,
        [ContactPhone] nvarchar(20) NOT NULL,
        [CommissionRate] decimal(5,2) NULL,
        [Status] nvarchar(30) NOT NULL DEFAULT N'PendingReview',
        [RejectionReason] nvarchar(500) NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [ApprovedBy] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Vendor] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Vendor_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [MasterProductImage] (
        [MasterProductId] uniqueidentifier NOT NULL,
        [ImageBankId] uniqueidentifier NOT NULL,
        [DisplayOrder] int NOT NULL DEFAULT 0,
        [IsPrimary] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_MasterProductImage] PRIMARY KEY ([MasterProductId], [ImageBankId]),
        CONSTRAINT [FK_MasterProductImage_ImageBank_ImageBankId] FOREIGN KEY ([ImageBankId]) REFERENCES [ImageBank] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MasterProductImage_MasterProduct_MasterProductId] FOREIGN KEY ([MasterProductId]) REFERENCES [MasterProduct] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [DriverLocations] (
        [Id] uniqueidentifier NOT NULL,
        [DriverId] uniqueidentifier NOT NULL,
        [Latitude] decimal(10,7) NOT NULL,
        [Longitude] decimal(10,7) NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DriverLocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DriverLocations_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Carts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [DiscountTotal] decimal(18,2) NOT NULL,
        [DeliveryFee] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Carts_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Carts_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [CouponVendors] (
        [Id] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CouponVendors] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CouponVendors_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CouponVendors_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Settlements] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NULL,
        [DriverId] uniqueidentifier NULL,
        [Status] nvarchar(50) NOT NULL,
        [GrossAmount] decimal(18,2) NOT NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [ProcessedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Settlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Settlements_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Settlements_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [VendorBankAccount] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [BankName] nvarchar(200) NOT NULL,
        [AccountHolderName] nvarchar(200) NOT NULL,
        [IBAN] nvarchar(34) NOT NULL,
        [SwiftCode] nvarchar(11) NULL,
        [IsPrimary] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Status] nvarchar(30) NOT NULL DEFAULT N'PendingVerification',
        [RejectionReason] nvarchar(500) NULL,
        [VerifiedAtUtc] datetime2 NULL,
        [VerifiedBy] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorBankAccount] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorBankAccount_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [VendorBranch] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [AddressLine] nvarchar(500) NOT NULL,
        [Latitude] decimal(9,6) NOT NULL,
        [Longitude] decimal(9,6) NOT NULL,
        [ContactPhone] nvarchar(20) NOT NULL,
        [DeliveryRadiusKm] decimal(5,2) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorBranch] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorBranch_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Payouts] (
        [Id] uniqueidentifier NOT NULL,
        [SettlementId] uniqueidentifier NOT NULL,
        [VendorBankAccountId] uniqueidentifier NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [TransferReference] nvarchar(200) NULL,
        [ProcessedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Payouts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payouts_Settlements_SettlementId] FOREIGN KEY ([SettlementId]) REFERENCES [Settlements] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Payouts_VendorBankAccount_VendorBankAccountId] FOREIGN KEY ([VendorBankAccountId]) REFERENCES [VendorBankAccount] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [BranchOperatingHour] (
        [Id] uniqueidentifier NOT NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [DayOfWeek] int NOT NULL,
        [OpenTime] time NOT NULL,
        [CloseTime] time NOT NULL,
        [IsClosed] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_BranchOperatingHour] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BranchOperatingHour_VendorBranch_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [VendorBranch] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] uniqueidentifier NOT NULL,
        [OrderNumber] nvarchar(50) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [VendorBranchId] uniqueidentifier NULL,
        [CustomerAddressId] uniqueidentifier NOT NULL,
        [CouponId] uniqueidentifier NULL,
        [Status] nvarchar(50) NOT NULL,
        [PaymentMethod] nvarchar(50) NOT NULL,
        [PaymentStatus] nvarchar(50) NOT NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [DiscountTotal] decimal(18,2) NOT NULL,
        [DeliveryFee] decimal(18,2) NOT NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [PlacedAtUtc] datetime2 NOT NULL,
        [DeliveredAtUtc] datetime2 NULL,
        [CancelledAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Orders_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_VendorBranch_VendorBranchId] FOREIGN KEY ([VendorBranchId]) REFERENCES [VendorBranch] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [VendorProduct] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [MasterProductId] uniqueidentifier NOT NULL,
        [VendorBranchId] uniqueidentifier NULL,
        [SellingPrice] decimal(18,2) NOT NULL,
        [CompareAtPrice] decimal(18,2) NULL,
        [StockQuantity] int NOT NULL DEFAULT 0,
        [IsAvailable] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Status] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorProduct] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorProduct_MasterProduct_MasterProductId] FOREIGN KEY ([MasterProductId]) REFERENCES [MasterProduct] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VendorProduct_VendorBranch_VendorBranchId] FOREIGN KEY ([VendorBranchId]) REFERENCES [VendorBranch] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VendorProduct_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [DeliveryAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [DriverId] uniqueidentifier NULL,
        [Status] nvarchar(50) NOT NULL,
        [OfferedAtUtc] datetime2 NULL,
        [AcceptedAtUtc] datetime2 NULL,
        [PickedUpAtUtc] datetime2 NULL,
        [DeliveredAtUtc] datetime2 NULL,
        [FailedAtUtc] datetime2 NULL,
        [FailureReason] nvarchar(300) NULL,
        [CodAmount] decimal(18,2) NOT NULL DEFAULT 0.0,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DeliveryAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeliveryAssignments_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DeliveryAssignments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [OrderStatusHistories] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [OldStatus] nvarchar(50) NULL,
        [NewStatus] nvarchar(50) NOT NULL,
        [ChangedByUserId] uniqueidentifier NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderStatusHistories_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderStatusHistories_User_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [User] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Method] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [ProviderName] nvarchar(100) NULL,
        [ProviderTransactionId] nvarchar(200) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaidAtUtc] datetime2 NULL,
        [FailedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reviews_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reviews_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [SettlementItems] (
        [Id] uniqueidentifier NOT NULL,
        [SettlementId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [VendorAmount] decimal(18,2) NOT NULL,
        [DriverAmount] decimal(18,2) NOT NULL,
        [PlatformCommission] decimal(18,2) NOT NULL,
        [CodCollectedAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_SettlementItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SettlementItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SettlementItems_Settlements_SettlementId] FOREIGN KEY ([SettlementId]) REFERENCES [Settlements] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [CartItems] (
        [Id] uniqueidentifier NOT NULL,
        [CartId] uniqueidentifier NOT NULL,
        [VendorProductId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CartItems_VendorProduct_VendorProductId] FOREIGN KEY ([VendorProductId]) REFERENCES [VendorProduct] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [VendorProductId] uniqueidentifier NOT NULL,
        [MasterProductId] uniqueidentifier NOT NULL,
        [ProductName] nvarchar(250) NOT NULL,
        [UnitName] nvarchar(100) NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [LineDiscount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_MasterProduct_MasterProductId] FOREIGN KEY ([MasterProductId]) REFERENCES [MasterProduct] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderItems_VendorProduct_VendorProductId] FOREIGN KEY ([VendorProductId]) REFERENCES [VendorProduct] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [DeliveryProofs] (
        [Id] uniqueidentifier NOT NULL,
        [AssignmentId] uniqueidentifier NOT NULL,
        [ProofType] nvarchar(50) NOT NULL,
        [ImageUrl] nvarchar(500) NULL,
        [OtpCode] nvarchar(50) NULL,
        [RecipientName] nvarchar(200) NULL,
        [Note] nvarchar(300) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DeliveryProofs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeliveryProofs_DeliveryAssignments_AssignmentId] FOREIGN KEY ([AssignmentId]) REFERENCES [DeliveryAssignments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [Refunds] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reason] nvarchar(300) NULL,
        [Status] nvarchar(50) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Refunds_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE TABLE [WalletTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NULL,
        [PaymentId] uniqueidentifier NULL,
        [SettlementId] uniqueidentifier NULL,
        [TxnType] nvarchar(50) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Direction] nvarchar(10) NOT NULL,
        [ReferenceType] nvarchar(100) NULL,
        [ReferenceId] uniqueidentifier NULL,
        [Description] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_WalletTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WalletTransactions_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WalletTransactions_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WalletTransactions_Settlements_SettlementId] FOREIGN KEY ([SettlementId]) REFERENCES [Settlements] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WalletTransactions_Wallet_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [Wallet] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BranchOpHour_Branch_Day] ON [BranchOperatingHour] ([BranchId], [DayOfWeek]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CartItems_CartId_VendorProductId] ON [CartItems] ([CartId], [VendorProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_CartItems_VendorProductId] ON [CartItems] ([VendorProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Carts_UserId_VendorId] ON [Carts] ([UserId], [VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Carts_VendorId] ON [Carts] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Category_ParentId] ON [Category] ([ParentCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CouponVendors_CouponId_VendorId] ON [CouponVendors] ([CouponId], [VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_CouponVendors_VendorId] ON [CouponVendors] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerAddresses_UserId] ON [CustomerAddresses] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_DeliveryAssignments_DriverId] ON [DeliveryAssignments] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_DeliveryAssignments_OrderId] ON [DeliveryAssignments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_DeliveryProofs_AssignmentId] ON [DeliveryProofs] ([AssignmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_DriverLocations_DriverId] ON [DriverLocations] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Drivers_UserId] ON [Drivers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MasterProduct_Barcode] ON [MasterProduct] ([Barcode]) WHERE [Barcode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_MasterProduct_BrandId] ON [MasterProduct] ([BrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_MasterProduct_CategoryId] ON [MasterProduct] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_MasterProduct_UnitOfMeasureId] ON [MasterProduct] ([UnitOfMeasureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_MasterProductImage_ImageBankId] ON [MasterProductImage] ([ImageBankId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_MasterProductId] ON [OrderItems] ([MasterProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_VendorProductId] ON [OrderItems] ([VendorProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [Orders] ([OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_VendorBranchId] ON [Orders] ([VendorBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_VendorId] ON [Orders] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_ChangedByUserId] ON [OrderStatusHistories] ([ChangedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_OrderId] ON [OrderStatusHistories] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payouts_SettlementId] ON [Payouts] ([SettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payouts_VendorBankAccountId] ON [Payouts] ([VendorBankAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshToken_Token] ON [RefreshToken] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshToken_UserId] ON [RefreshToken] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Refunds_PaymentId] ON [Refunds] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_OrderId] ON [Reviews] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_VendorId] ON [Reviews] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_SettlementItems_OrderId] ON [SettlementItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_SettlementItems_SettlementId] ON [SettlementItems] ([SettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Settlements_DriverId] ON [Settlements] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Settlements_VendorId] ON [Settlements] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_User_Email] ON [User] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_User_Phone] ON [User] ([Phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vendor_CommRegNum] ON [Vendor] ([CommercialRegistrationNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vendor_Status] ON [Vendor] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Vendor_UserId] ON [Vendor] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_VendorBankAccount_VendorId] ON [VendorBankAccount] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_VendorBranch_VendorId] ON [VendorBranch] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_VendorProduct_MasterProductId] ON [VendorProduct] ([MasterProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorProduct_Vendor_Master] ON [VendorProduct] ([VendorId], [MasterProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_VendorProduct_VendorBranchId] ON [VendorProduct] ([VendorBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_VendorProduct_VendorId] ON [VendorProduct] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Wallet_Owner] ON [Wallet] ([OwnerType], [OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTransactions_OrderId] ON [WalletTransactions] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTransactions_PaymentId] ON [WalletTransactions] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTransactions_SettlementId] ON [WalletTransactions] ([SettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    CREATE INDEX [IX_WalletTransactions_WalletId] ON [WalletTransactions] ([WalletId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260301144941_20260301_1650_GlobalInitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260301144941_20260301_1650_GlobalInitialCreate', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302152350_InitialIdentityMigration'
)
BEGIN
    DROP INDEX [IX_RefreshToken_Token] ON [RefreshToken];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302152350_InitialIdentityMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshToken_Token] ON [RefreshToken] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302152350_InitialIdentityMigration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260302152350_InitialIdentityMigration', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [Vendor] ADD [CommercialRegisterDocumentUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [Vendor] ADD [LogoUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [User] ADD [Address] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [User] ADD [Latitude] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [User] ADD [Longitude] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [User] ADD [ProfilePhotoUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [Drivers] ADD [Address] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [Drivers] ADD [LicenseImageUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [Drivers] ADD [NationalIdImageUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [Drivers] ADD [PersonalPhotoUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    ALTER TABLE [Drivers] ADD [VehicleImageUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260302165921_RegistrationFlows'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260302165921_RegistrationFlows', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304035546_AddressLabelEnum'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerAddresses]') AND [c].[name] = N'Label');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [CustomerAddresses] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [CustomerAddresses] ALTER COLUMN [Label] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304035546_AddressLabelEnum'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260304035546_AddressLabelEnum', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304040120_MovedCustomerAddressToIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260304040120_MovedCustomerAddressToIdentity', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304042550_AddUserOtpFields'
)
BEGIN
    ALTER TABLE [User] ADD [OtpCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304042550_AddUserOtpFields'
)
BEGIN
    ALTER TABLE [User] ADD [OtpExpiryTime] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304042550_AddUserOtpFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260304042550_AddUserOtpFields', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304150515_RemoveRedundantUserAddressFields'
)
BEGIN
    ALTER TABLE [User] ADD [PasswordResetOtp] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304150515_RemoveRedundantUserAddressFields'
)
BEGIN
    ALTER TABLE [User] ADD [PasswordResetOtpExpiry] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260304150515_RemoveRedundantUserAddressFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260304150515_RemoveRedundantUserAddressFields', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    ALTER TABLE [VendorProduct] ADD [CustomDescriptionAr] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    ALTER TABLE [VendorProduct] ADD [CustomDescriptionEn] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    ALTER TABLE [VendorProduct] ADD [CustomNameAr] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    ALTER TABLE [VendorProduct] ADD [CustomNameEn] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    ALTER TABLE [ImageBank] ADD [RejectionReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    ALTER TABLE [ImageBank] ADD [Status] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    ALTER TABLE [ImageBank] ADD [UploadedByVendorId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    CREATE TABLE [ProductRequest] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [SuggestedNameAr] nvarchar(200) NOT NULL,
        [SuggestedNameEn] nvarchar(200) NOT NULL,
        [SuggestedDescriptionAr] nvarchar(1000) NULL,
        [SuggestedDescriptionEn] nvarchar(1000) NULL,
        [SuggestedCategoryId] uniqueidentifier NOT NULL,
        [ImageUrl] nvarchar(1000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [RejectionReason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductRequest] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductRequest_Category_SuggestedCategoryId] FOREIGN KEY ([SuggestedCategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductRequest_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    CREATE INDEX [IX_ImageBank_Status] ON [ImageBank] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    CREATE INDEX [IX_ImageBank_UploadedByVendorId] ON [ImageBank] ([UploadedByVendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    CREATE INDEX [IX_ProductRequest_Status] ON [ProductRequest] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    CREATE INDEX [IX_ProductRequest_SuggestedCategoryId] ON [ProductRequest] ([SuggestedCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    CREATE INDEX [IX_ProductRequest_VendorId] ON [ProductRequest] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131832_AddCatalogProductRequestsAndVendorOverrides'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260305131832_AddCatalogProductRequestsAndVendorOverrides', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305131954_AddCatalogCategoryProductRequests_V2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260305131954_AddCatalogCategoryProductRequests_V2', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305134014_AddBrandAndUnitBilingualSupport'
)
BEGIN
    EXEC sp_rename N'[Brand].[Name]', N'NameEn', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305134014_AddBrandAndUnitBilingualSupport'
)
BEGIN
    ALTER TABLE [UnitOfMeasure] ADD [Symbol] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305134014_AddBrandAndUnitBilingualSupport'
)
BEGIN
    ALTER TABLE [Brand] ADD [NameAr] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260305134014_AddBrandAndUnitBilingualSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260305134014_AddBrandAndUnitBilingualSupport', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Carts] DROP CONSTRAINT [FK_Carts_User_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [CustomerAddresses] DROP CONSTRAINT [FK_CustomerAddresses_User_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Drivers] DROP CONSTRAINT [FK_Drivers_User_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Notifications] DROP CONSTRAINT [FK_Notifications_User_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Orders] DROP CONSTRAINT [FK_Orders_User_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [OrderStatusHistories] DROP CONSTRAINT [FK_OrderStatusHistories_User_ChangedByUserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [RefreshToken] DROP CONSTRAINT [FK_RefreshToken_User_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Reviews] DROP CONSTRAINT [FK_Reviews_User_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Vendor] DROP CONSTRAINT [FK_Vendor_User_UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [User] DROP CONSTRAINT [PK_User];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    DROP INDEX [IX_User_Email] ON [User];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    DROP INDEX [IX_User_Phone] ON [User];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[User]') AND [c].[name] = N'IsEmailVerified');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [User] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [User] DROP COLUMN [IsEmailVerified];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[User]') AND [c].[name] = N'IsPhoneVerified');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [User] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [User] DROP COLUMN [IsPhoneVerified];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[User]') AND [c].[name] = N'Phone');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [User] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [User] DROP COLUMN [Phone];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    EXEC sp_rename N'[User]', N'AspNetUsers', 'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'PasswordHash');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [AspNetUsers] ALTER COLUMN [PasswordHash] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Email');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [AspNetUsers] ALTER COLUMN [Email] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [AccessFailedCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ConcurrencyStamp] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [EmailConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LockoutEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LockoutEnd] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [NormalizedEmail] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [NormalizedUserName] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PhoneNumber] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PhoneNumberConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [SecurityStamp] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TwoFactorEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [UserName] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Carts] ADD CONSTRAINT [FK_Carts_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [CustomerAddresses] ADD CONSTRAINT [FK_CustomerAddresses_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Drivers] ADD CONSTRAINT [FK_Drivers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [OrderStatusHistories] ADD CONSTRAINT [FK_OrderStatusHistories_AspNetUsers_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [RefreshToken] ADD CONSTRAINT [FK_RefreshToken_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Reviews] ADD CONSTRAINT [FK_Reviews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    ALTER TABLE [Vendor] ADD CONSTRAINT [FK_Vendor_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307102657_InitialIdentitySchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260307102657_InitialIdentitySchema', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307135750_AddCategoryImageUrl'
)
BEGIN
    ALTER TABLE [Category] ADD [ImageUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307135750_AddCategoryImageUrl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260307135750_AddCategoryImageUrl', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260307145408_ConsolidatePendingSets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260307145408_ConsolidatePendingSets', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308092338_UnifyProductAssets'
)
BEGIN
    ALTER TABLE [MasterProductImage] DROP CONSTRAINT [FK_MasterProductImage_ImageBank_ImageBankId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308092338_UnifyProductAssets'
)
BEGIN
    DROP TABLE [ImageBank];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308092338_UnifyProductAssets'
)
BEGIN
    ALTER TABLE [MasterProductImage] DROP CONSTRAINT [PK_MasterProductImage];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308092338_UnifyProductAssets'
)
BEGIN
    DROP INDEX [IX_MasterProductImage_ImageBankId] ON [MasterProductImage];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308092338_UnifyProductAssets'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MasterProductImage]') AND [c].[name] = N'ImageBankId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [MasterProductImage] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [MasterProductImage] DROP COLUMN [ImageBankId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308092338_UnifyProductAssets'
)
BEGIN
    ALTER TABLE [MasterProductImage] ADD [Url] nvarchar(2048) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308092338_UnifyProductAssets'
)
BEGIN
    ALTER TABLE [MasterProductImage] ADD [AltText] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308092338_UnifyProductAssets'
)
BEGIN
    ALTER TABLE [MasterProductImage] ADD CONSTRAINT [PK_MasterProductImage] PRIMARY KEY ([MasterProductId], [Url]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260308092338_UnifyProductAssets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260308092338_UnifyProductAssets', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260309142633_AddLastOtpSentAt'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LastOtpSentAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260309142633_AddLastOtpSentAt'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260309142633_AddLastOtpSentAt', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312132141_AddSlugColumnToMasterProduct'
)
BEGIN

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MasterProduct]') AND name = 'Slug')
                    BEGIN
                        ALTER TABLE [MasterProduct] ADD [Slug] nvarchar(300) NOT NULL DEFAULT N'';
                    END
                    ELSE
                    BEGIN
                        DECLARE @varSlug sysname;
                        SELECT @varSlug = [d].[name]
                        FROM [sys].[default_constraints] [d]
                        INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                        WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MasterProduct]') AND [c].[name] = N'Slug');
                        IF @varSlug IS NOT NULL EXEC(N'ALTER TABLE [MasterProduct] DROP CONSTRAINT [' + @varSlug + '];');
                        ALTER TABLE [MasterProduct] ALTER COLUMN [Slug] nvarchar(300) NOT NULL;
                    END
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312132141_AddSlugColumnToMasterProduct'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MasterProduct_Slug] ON [MasterProduct] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312132141_AddSlugColumnToMasterProduct'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260312132141_AddSlugColumnToMasterProduct', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [CartItems] DROP CONSTRAINT [FK_CartItems_VendorProduct_VendorProductId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Carts] DROP CONSTRAINT [FK_Carts_Vendor_VendorId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    DROP INDEX [IX_Carts_UserId_VendorId] ON [Carts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    DROP INDEX [IX_Carts_VendorId] ON [Carts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Carts]') AND [c].[name] = N'VendorId');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Carts] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [Carts] DROP COLUMN [VendorId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CartItems]') AND [c].[name] = N'LineTotal');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [CartItems] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [CartItems] DROP COLUMN [LineTotal];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CartItems]') AND [c].[name] = N'UnitPrice');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [CartItems] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [CartItems] DROP COLUMN [UnitPrice];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    EXEC sp_rename N'[CartItems].[VendorProductId]', N'MasterProductId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    EXEC sp_rename N'[CartItems].[IX_CartItems_VendorProductId]', N'IX_CartItems_MasterProductId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    EXEC sp_rename N'[CartItems].[IX_CartItems_CartId_VendorProductId]', N'IX_CartItems_CartId_MasterProductId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [ApprovalNote] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [ArchiveReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [ArchivedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [City] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [CommercialRegistrationExpiryDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [DescriptionAr] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [DescriptionEn] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [IdNumber] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [LastStatusChangedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [LicenseNumber] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [LockReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [LockedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [NationalAddress] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [Nationality] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [OwnerEmail] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [OwnerName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [OwnerPhone] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [PayoutCycle] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [Region] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [SuspendedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [SuspensionReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [CartItems] ADD [ProductName] nvarchar(250) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ArchiveReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ArchivedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [IsLoginLocked] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LockReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LockedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    ALTER TABLE [CartItems] ADD CONSTRAINT [FK_CartItems_MasterProduct_MasterProductId] FOREIGN KEY ([MasterProductId]) REFERENCES [MasterProduct] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406121251_ExpandVendorWorkspaceAndAdminControls'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260406121251_ExpandVendorWorkspaceAndAdminControls', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407102830_SyncVendorOperationalAndNotificationSettings'
)
BEGIN
    ALTER TABLE [Vendor] ADD [AcceptOrders] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407102830_SyncVendorOperationalAndNotificationSettings'
)
BEGIN
    ALTER TABLE [Vendor] ADD [EmailNotificationsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407102830_SyncVendorOperationalAndNotificationSettings'
)
BEGIN
    ALTER TABLE [Vendor] ADD [MinimumOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407102830_SyncVendorOperationalAndNotificationSettings'
)
BEGIN
    ALTER TABLE [Vendor] ADD [NewOrdersNotificationsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407102830_SyncVendorOperationalAndNotificationSettings'
)
BEGIN
    ALTER TABLE [Vendor] ADD [PreparationTimeMinutes] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407102830_SyncVendorOperationalAndNotificationSettings'
)
BEGIN
    ALTER TABLE [Vendor] ADD [SmsNotificationsEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407102830_SyncVendorOperationalAndNotificationSettings'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Longitude');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [AspNetUsers] ALTER COLUMN [Longitude] decimal(9,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407102830_SyncVendorOperationalAndNotificationSettings'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Latitude');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [AspNetUsers] ALTER COLUMN [Latitude] decimal(9,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407102830_SyncVendorOperationalAndNotificationSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260407102830_SyncVendorOperationalAndNotificationSettings', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductRequest]') AND [c].[name] = N'SuggestedCategoryId');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [ProductRequest] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [ProductRequest] ALTER COLUMN [SuggestedCategoryId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [CreatedMasterProductId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [ReviewedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [ReviewedBy] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [SuggestedBrandId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [SuggestedBrandRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [SuggestedCategoryRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [SuggestedUnitOfMeasureId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE TABLE [BrandRequest] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [LogoUrl] nvarchar(1000) NULL,
        [Status] nvarchar(20) NOT NULL,
        [RejectionReason] nvarchar(500) NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [ReviewedBy] nvarchar(200) NULL,
        [CreatedBrandId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_BrandRequest] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BrandRequest_Brand_CreatedBrandId] FOREIGN KEY ([CreatedBrandId]) REFERENCES [Brand] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BrandRequest_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE TABLE [CategoryRequest] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [ImageUrl] nvarchar(1000) NULL,
        [ParentCategoryId] uniqueidentifier NULL,
        [DisplayOrder] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RejectionReason] nvarchar(500) NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [ReviewedBy] nvarchar(200) NULL,
        [CreatedCategoryId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CategoryRequest] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CategoryRequest_Category_CreatedCategoryId] FOREIGN KEY ([CreatedCategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CategoryRequest_Category_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CategoryRequest_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_ProductRequest_CreatedMasterProductId] ON [ProductRequest] ([CreatedMasterProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_ProductRequest_SuggestedBrandId] ON [ProductRequest] ([SuggestedBrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_ProductRequest_SuggestedBrandRequestId] ON [ProductRequest] ([SuggestedBrandRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_ProductRequest_SuggestedCategoryRequestId] ON [ProductRequest] ([SuggestedCategoryRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_ProductRequest_SuggestedUnitOfMeasureId] ON [ProductRequest] ([SuggestedUnitOfMeasureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_BrandRequest_CreatedBrandId] ON [BrandRequest] ([CreatedBrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_BrandRequest_Status] ON [BrandRequest] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_BrandRequest_VendorId] ON [BrandRequest] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_CategoryRequest_CreatedCategoryId] ON [CategoryRequest] ([CreatedCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_CategoryRequest_ParentCategoryId] ON [CategoryRequest] ([ParentCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_CategoryRequest_Status] ON [CategoryRequest] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    CREATE INDEX [IX_CategoryRequest_VendorId] ON [CategoryRequest] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD CONSTRAINT [FK_ProductRequest_BrandRequest_SuggestedBrandRequestId] FOREIGN KEY ([SuggestedBrandRequestId]) REFERENCES [BrandRequest] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD CONSTRAINT [FK_ProductRequest_Brand_SuggestedBrandId] FOREIGN KEY ([SuggestedBrandId]) REFERENCES [Brand] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD CONSTRAINT [FK_ProductRequest_CategoryRequest_SuggestedCategoryRequestId] FOREIGN KEY ([SuggestedCategoryRequestId]) REFERENCES [CategoryRequest] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD CONSTRAINT [FK_ProductRequest_MasterProduct_CreatedMasterProductId] FOREIGN KEY ([CreatedMasterProductId]) REFERENCES [MasterProduct] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD CONSTRAINT [FK_ProductRequest_UnitOfMeasure_SuggestedUnitOfMeasureId] FOREIGN KEY ([SuggestedUnitOfMeasureId]) REFERENCES [UnitOfMeasure] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260407134410_AddBrandCategoryRequestsWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260407134410_AddBrandCategoryRequestsWorkflow', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408075644_AddHomeContentBanners'
)
BEGIN
    CREATE TABLE [HomeBanner] (
        [Id] uniqueidentifier NOT NULL,
        [TagAr] nvarchar(100) NOT NULL,
        [TagEn] nvarchar(100) NOT NULL,
        [TitleAr] nvarchar(200) NOT NULL,
        [TitleEn] nvarchar(200) NOT NULL,
        [SubtitleAr] nvarchar(500) NULL,
        [SubtitleEn] nvarchar(500) NULL,
        [ActionLabelAr] nvarchar(100) NULL,
        [ActionLabelEn] nvarchar(100) NULL,
        [ImageUrl] nvarchar(2048) NOT NULL,
        [DisplayOrder] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [StartsAtUtc] datetime2 NULL,
        [EndsAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_HomeBanner] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408075644_AddHomeContentBanners'
)
BEGIN
    CREATE INDEX [IX_HomeBanner_IsActive_DisplayOrder] ON [HomeBanner] ([IsActive], [DisplayOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408075644_AddHomeContentBanners'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408075644_AddHomeContentBanners', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408081728_AddFeaturedProductPlacements'
)
BEGIN
    CREATE TABLE [FeaturedProductPlacement] (
        [Id] uniqueidentifier NOT NULL,
        [PlacementType] nvarchar(30) NOT NULL,
        [VendorProductId] uniqueidentifier NULL,
        [MasterProductId] uniqueidentifier NULL,
        [DisplayOrder] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [StartsAtUtc] datetime2 NULL,
        [EndsAtUtc] datetime2 NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_FeaturedProductPlacement] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeaturedProductPlacement_MasterProduct_MasterProductId] FOREIGN KEY ([MasterProductId]) REFERENCES [MasterProduct] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeaturedProductPlacement_VendorProduct_VendorProductId] FOREIGN KEY ([VendorProductId]) REFERENCES [VendorProduct] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408081728_AddFeaturedProductPlacements'
)
BEGIN
    CREATE INDEX [IX_FeaturedProductPlacement_IsActive_DisplayOrder] ON [FeaturedProductPlacement] ([IsActive], [DisplayOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408081728_AddFeaturedProductPlacements'
)
BEGIN
    CREATE INDEX [IX_FeaturedProductPlacement_MasterProductId] ON [FeaturedProductPlacement] ([MasterProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408081728_AddFeaturedProductPlacements'
)
BEGIN
    CREATE INDEX [IX_FeaturedProductPlacement_Target] ON [FeaturedProductPlacement] ([PlacementType], [VendorProductId], [MasterProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408081728_AddFeaturedProductPlacements'
)
BEGIN
    CREATE INDEX [IX_FeaturedProductPlacement_VendorProductId] ON [FeaturedProductPlacement] ([VendorProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408081728_AddFeaturedProductPlacements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408081728_AddFeaturedProductPlacements', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddHomeSections'
)
BEGIN
    CREATE TABLE [HomeSection] (
        [Id] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [Theme] nvarchar(100) NOT NULL,
        [DisplayOrder] int NOT NULL DEFAULT 0,
        [ProductsTake] int NOT NULL DEFAULT 10,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [StartsAtUtc] datetime2 NULL,
        [EndsAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_HomeSection] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HomeSection_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddHomeSections'
)
BEGIN
    CREATE INDEX [IX_HomeSection_CategoryId] ON [HomeSection] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddHomeSections'
)
BEGIN
    CREATE INDEX [IX_HomeSection_IsActive_DisplayOrder] ON [HomeSection] ([IsActive], [DisplayOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408120000_AddHomeSections'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408120000_AddHomeSections', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408121311_PendingCheckHomeModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408121311_PendingCheckHomeModel', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408133000_AddHomeContentSectionSettings'
)
BEGIN
    CREATE TABLE [HomeContentSectionSetting] (
        [Id] uniqueidentifier NOT NULL,
        [SectionType] nvarchar(50) NOT NULL,
        [IsEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_HomeContentSectionSetting] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408133000_AddHomeContentSectionSettings'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_HomeContentSectionSetting_SectionType] ON [HomeContentSectionSetting] ([SectionType]) WHERE [SectionType] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260408133000_AddHomeContentSectionSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260408133000_AddHomeContentSectionSettings', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD [PartId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD [ProductTypeId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    CREATE TABLE [ProductType] (
        [Id] uniqueidentifier NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductType] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductType_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    CREATE TABLE [Part] (
        [Id] uniqueidentifier NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [ProductTypeId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Part] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Part_ProductType_ProductTypeId] FOREIGN KEY ([ProductTypeId]) REFERENCES [ProductType] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    CREATE INDEX [IX_MasterProduct_PartId] ON [MasterProduct] ([PartId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    CREATE INDEX [IX_MasterProduct_ProductTypeId] ON [MasterProduct] ([ProductTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Part_ProductTypeId_NameEn] ON [Part] ([ProductTypeId], [NameEn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductType_CategoryId_NameEn] ON [ProductType] ([CategoryId], [NameEn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD CONSTRAINT [FK_MasterProduct_Part_PartId] FOREIGN KEY ([PartId]) REFERENCES [Part] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD CONSTRAINT [FK_MasterProduct_ProductType_ProductTypeId] FOREIGN KEY ([ProductTypeId]) REFERENCES [ProductType] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409124211_AddCatalogProductTypesAndParts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409124211_AddCatalogProductTypesAndParts', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409131447_AddGuestCartSupport'
)
BEGIN
    DROP INDEX [IX_Carts_UserId] ON [Carts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409131447_AddGuestCartSupport'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Carts]') AND [c].[name] = N'UserId');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Carts] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [Carts] ALTER COLUMN [UserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409131447_AddGuestCartSupport'
)
BEGIN
    ALTER TABLE [Carts] ADD [GuestId] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409131447_AddGuestCartSupport'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Carts_GuestId] ON [Carts] ([GuestId]) WHERE [GuestId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409131447_AddGuestCartSupport'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Carts_UserId] ON [Carts] ([UserId]) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260409131447_AddGuestCartSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260409131447_AddGuestCartSupport', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410202226_AddBrandCategoryLink'
)
BEGIN
    ALTER TABLE [Brand] ADD [CategoryId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410202226_AddBrandCategoryLink'
)
BEGIN
    ;WITH RankedBrandCategories AS (
        SELECT
            mp.BrandId,
            mp.CategoryId,
            ROW_NUMBER() OVER (
                PARTITION BY mp.BrandId
                ORDER BY COUNT(*) DESC, MIN(mp.CreatedAtUtc) ASC
            ) AS Ranking
        FROM MasterProduct mp
        WHERE mp.BrandId IS NOT NULL
        GROUP BY mp.BrandId, mp.CategoryId
    )
    UPDATE b
    SET b.CategoryId = ranked.CategoryId
    FROM Brand b
    INNER JOIN RankedBrandCategories ranked
        ON ranked.BrandId = b.Id
       AND ranked.Ranking = 1
    WHERE b.CategoryId IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410202226_AddBrandCategoryLink'
)
BEGIN
    CREATE INDEX [IX_Brand_CategoryId] ON [Brand] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410202226_AddBrandCategoryLink'
)
BEGIN
    ALTER TABLE [Brand] ADD CONSTRAINT [FK_Brand_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260410202226_AddBrandCategoryLink'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260410202226_AddBrandCategoryLink', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411081852_AddCustomerFavorites'
)
BEGIN
    CREATE TABLE [CustomerFavorites] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [MasterProductId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerFavorites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerFavorites_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerFavorites_MasterProduct_MasterProductId] FOREIGN KEY ([MasterProductId]) REFERENCES [MasterProduct] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411081852_AddCustomerFavorites'
)
BEGIN
    CREATE INDEX [IX_CustomerFavorites_MasterProductId] ON [CustomerFavorites] ([MasterProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411081852_AddCustomerFavorites'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerFavorites_UserId_MasterProductId] ON [CustomerFavorites] ([UserId], [MasterProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411081852_AddCustomerFavorites'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411081852_AddCustomerFavorites', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411105256_AddCustomerPresenceRealtime'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LastSeenAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411105256_AddCustomerPresenceRealtime'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PresenceState] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411105256_AddCustomerPresenceRealtime'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411105256_AddCustomerPresenceRealtime', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411165000_AddGuestFavoritesSupport'
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_CustomerFavorites_UserId_MasterProductId'
          AND object_id = OBJECT_ID(N'[CustomerFavorites]')
    )
    BEGIN
        DROP INDEX [IX_CustomerFavorites_UserId_MasterProductId] ON [CustomerFavorites];
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411165000_AddGuestFavoritesSupport'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerFavorites]') AND [c].[name] = N'UserId');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [CustomerFavorites] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [CustomerFavorites] ALTER COLUMN [UserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411165000_AddGuestFavoritesSupport'
)
BEGIN
    ALTER TABLE [CustomerFavorites] ADD [GuestId] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411165000_AddGuestFavoritesSupport'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CustomerFavorites_GuestId_MasterProductId] ON [CustomerFavorites] ([GuestId], [MasterProductId]) WHERE [GuestId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411165000_AddGuestFavoritesSupport'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CustomerFavorites_UserId_MasterProductId] ON [CustomerFavorites] ([UserId], [MasterProductId]) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411165000_AddGuestFavoritesSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411165000_AddGuestFavoritesSupport', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260413104628_ConstrainHomeSectionThemes'
)
BEGIN
    UPDATE [HomeSection]
    SET [Theme] = CASE
        WHEN LOWER([Theme]) IN ('soft-blue', 'fresh-orange', 'bold-dark') THEN LOWER([Theme])
        WHEN LOWER([Theme]) = 'theme1' THEN 'soft-blue'
        WHEN LOWER([Theme]) = 'theme2' THEN 'fresh-orange'
        WHEN LOWER([Theme]) = 'theme3' THEN 'bold-dark'
        ELSE 'soft-blue'
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260413104628_ConstrainHomeSectionThemes'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[HomeSection]') AND [c].[name] = N'Theme');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [HomeSection] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [HomeSection] ALTER COLUMN [Theme] nvarchar(32) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260413104628_ConstrainHomeSectionThemes'
)
BEGIN
    EXEC(N'ALTER TABLE [HomeSection] ADD CONSTRAINT [CK_HomeSection_Theme] CHECK ([Theme] IN (''soft-blue'', ''fresh-orange'', ''bold-dark''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260413104628_ConstrainHomeSectionThemes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260413104628_ConstrainHomeSectionThemes', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    CREATE TABLE [VendorProductBulkOperation] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [TotalRows] int NOT NULL,
        [ProcessedRows] int NOT NULL,
        [SucceededRows] int NOT NULL,
        [FailedRows] int NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [StartedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorProductBulkOperation] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorProductBulkOperation_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    CREATE TABLE [VendorProductBulkOperationItem] (
        [Id] uniqueidentifier NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [RowNumber] int NOT NULL,
        [MasterProductId] uniqueidentifier NOT NULL,
        [VendorBranchId] uniqueidentifier NULL,
        [SellingPrice] decimal(18,2) NOT NULL,
        [CompareAtPrice] decimal(18,2) NULL,
        [StockQty] int NOT NULL,
        [Sku] nvarchar(100) NULL,
        [MinOrderQty] int NOT NULL,
        [MaxOrderQty] int NULL,
        [Status] nvarchar(30) NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [CreatedVendorProductId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorProductBulkOperationItem] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorProductBulkOperationItem_MasterProduct_MasterProductId] FOREIGN KEY ([MasterProductId]) REFERENCES [MasterProduct] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VendorProductBulkOperationItem_VendorBranch_VendorBranchId] FOREIGN KEY ([VendorBranchId]) REFERENCES [VendorBranch] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_VendorProductBulkOperationItem_VendorProductBulkOperation_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [VendorProductBulkOperation] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorProductBulkOperation_IdempotencyKey] ON [VendorProductBulkOperation] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    CREATE INDEX [IX_VendorProductBulkOperation_Status] ON [VendorProductBulkOperation] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    CREATE INDEX [IX_VendorProductBulkOperation_VendorId] ON [VendorProductBulkOperation] ([VendorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    CREATE INDEX [IX_VendorProductBulkOperationItem_MasterProductId] ON [VendorProductBulkOperationItem] ([MasterProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    CREATE INDEX [IX_VendorProductBulkOperationItem_OperationId] ON [VendorProductBulkOperationItem] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorProductBulkOperationItem_OperationId_RowNumber] ON [VendorProductBulkOperationItem] ([OperationId], [RowNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    CREATE INDEX [IX_VendorProductBulkOperationItem_VendorBranchId] ON [VendorProductBulkOperationItem] ([VendorBranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414093926_AddVendorProductBulkOperations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260414093926_AddVendorProductBulkOperations', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414112553_AddAdminMasterProductBulkOperations'
)
BEGIN
    CREATE TABLE [AdminMasterProductBulkOperations] (
        [Id] uniqueidentifier NOT NULL,
        [AdminUserId] uniqueidentifier NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [Status] int NOT NULL,
        [TotalRows] int NOT NULL,
        [ProcessedRows] int NOT NULL,
        [SucceededRows] int NOT NULL,
        [FailedRows] int NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [StartedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminMasterProductBulkOperations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414112553_AddAdminMasterProductBulkOperations'
)
BEGIN
    CREATE TABLE [AdminMasterProductBulkOperationItems] (
        [Id] uniqueidentifier NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [RowNumber] int NOT NULL,
        [NameAr] nvarchar(250) NOT NULL,
        [NameEn] nvarchar(250) NOT NULL,
        [Slug] nvarchar(250) NOT NULL,
        [Barcode] nvarchar(100) NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [BrandId] uniqueidentifier NULL,
        [UnitId] uniqueidentifier NULL,
        [StatusValue] int NOT NULL,
        [DescriptionAr] nvarchar(2000) NULL,
        [DescriptionEn] nvarchar(2000) NULL,
        [Status] int NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [CreatedMasterProductId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminMasterProductBulkOperationItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AdminMasterProductBulkOperationItems_AdminMasterProductBulkOperations_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [AdminMasterProductBulkOperations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AdminMasterProductBulkOperationItems_Brand_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brand] ([Id]),
        CONSTRAINT [FK_AdminMasterProductBulkOperationItems_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id]),
        CONSTRAINT [FK_AdminMasterProductBulkOperationItems_UnitOfMeasure_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [UnitOfMeasure] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414112553_AddAdminMasterProductBulkOperations'
)
BEGIN
    CREATE INDEX [IX_AdminMasterProductBulkOperationItems_BrandId] ON [AdminMasterProductBulkOperationItems] ([BrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414112553_AddAdminMasterProductBulkOperations'
)
BEGIN
    CREATE INDEX [IX_AdminMasterProductBulkOperationItems_CategoryId] ON [AdminMasterProductBulkOperationItems] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414112553_AddAdminMasterProductBulkOperations'
)
BEGIN
    CREATE INDEX [IX_AdminMasterProductBulkOperationItems_OperationId] ON [AdminMasterProductBulkOperationItems] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414112553_AddAdminMasterProductBulkOperations'
)
BEGIN
    CREATE INDEX [IX_AdminMasterProductBulkOperationItems_UnitId] ON [AdminMasterProductBulkOperationItems] ([UnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414112553_AddAdminMasterProductBulkOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AdminMasterProductBulkOperations_AdminUserId_IdempotencyKey] ON [AdminMasterProductBulkOperations] ([AdminUserId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414112553_AddAdminMasterProductBulkOperations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260414112553_AddAdminMasterProductBulkOperations', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414150604_AddAdminBrandBulkOperations'
)
BEGIN
    CREATE TABLE [AdminBrandBulkOperations] (
        [Id] uniqueidentifier NOT NULL,
        [AdminUserId] uniqueidentifier NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [Status] int NOT NULL,
        [TotalRows] int NOT NULL,
        [ProcessedRows] int NOT NULL,
        [SucceededRows] int NOT NULL,
        [FailedRows] int NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [StartedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminBrandBulkOperations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414150604_AddAdminBrandBulkOperations'
)
BEGIN
    CREATE TABLE [AdminBrandBulkOperationItems] (
        [Id] uniqueidentifier NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [RowNumber] int NOT NULL,
        [NameAr] nvarchar(200) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [LogoUrl] nvarchar(500) NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [Status] int NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [CreatedBrandId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminBrandBulkOperationItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AdminBrandBulkOperationItems_AdminBrandBulkOperations_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [AdminBrandBulkOperations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AdminBrandBulkOperationItems_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414150604_AddAdminBrandBulkOperations'
)
BEGIN
    CREATE INDEX [IX_AdminBrandBulkOperationItems_CategoryId] ON [AdminBrandBulkOperationItems] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414150604_AddAdminBrandBulkOperations'
)
BEGIN
    CREATE INDEX [IX_AdminBrandBulkOperationItems_OperationId] ON [AdminBrandBulkOperationItems] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414150604_AddAdminBrandBulkOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AdminBrandBulkOperations_AdminUserId_IdempotencyKey] ON [AdminBrandBulkOperations] ([AdminUserId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414150604_AddAdminBrandBulkOperations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260414150604_AddAdminBrandBulkOperations', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414153322_AddAdminMasterProductBulkImages'
)
BEGIN
    ALTER TABLE [AdminMasterProductBulkOperationItems] ADD [ImagesJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260414153322_AddAdminMasterProductBulkImages'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260414153322_AddAdminMasterProductBulkImages', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    DROP INDEX [IX_Notifications_UserId] ON [Notifications];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    EXEC sp_rename N'[Notifications].[Title]', N'TitleEn', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    EXEC sp_rename N'[Notifications].[Body]', N'BodyEn', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    ALTER TABLE [Notifications] ADD [BodyAr] nvarchar(1000) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    ALTER TABLE [Notifications] ADD [Data] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    ALTER TABLE [Notifications] ADD [ReferenceId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    ALTER TABLE [Notifications] ADD [TitleAr] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    CREATE TABLE [OrderComplaints] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderComplaints] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderComplaints_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    CREATE TABLE [OrderComplaintAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [OrderComplaintId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FileUrl] nvarchar(2000) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderComplaintAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderComplaintAttachments_OrderComplaints_OrderComplaintId] FOREIGN KEY ([OrderComplaintId]) REFERENCES [OrderComplaints] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    CREATE INDEX [IX_Notifications_CreatedAtUtc] ON [Notifications] ([CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead] ON [Notifications] ([UserId], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    CREATE INDEX [IX_OrderComplaintAttachments_OrderComplaintId] ON [OrderComplaintAttachments] ([OrderComplaintId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrderComplaints_OrderId] ON [OrderComplaints] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416180521_UpdateNotificationForRealtime'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260416180521_UpdateNotificationForRealtime', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417140809_AddCategoryRequestTargetLevel'
)
BEGIN
    ALTER TABLE [CategoryRequest] ADD [TargetLevel] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417140809_AddCategoryRequestTargetLevel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260417140809_AddCategoryRequestTargetLevel', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    ALTER TABLE [BrandRequest] ADD [CategoryId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    UPDATE br
    SET br.CategoryId = b.CategoryId
    FROM BrandRequest br
    INNER JOIN Brand b ON b.Id = br.CreatedBrandId
    WHERE br.CategoryId IS NULL
      AND b.CategoryId IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    UPDATE br
    SET br.CategoryId = pr.SuggestedCategoryId
    FROM BrandRequest br
    INNER JOIN ProductRequest pr ON pr.SuggestedBrandRequestId = br.Id
    WHERE br.CategoryId IS NULL
      AND pr.SuggestedCategoryId IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    UPDATE br
    SET br.CategoryId = cr.CreatedCategoryId
    FROM BrandRequest br
    INNER JOIN ProductRequest pr ON pr.SuggestedBrandRequestId = br.Id
    INNER JOIN CategoryRequest cr ON cr.Id = pr.SuggestedCategoryRequestId
    WHERE br.CategoryId IS NULL
      AND cr.CreatedCategoryId IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    UPDATE br
    SET br.CategoryId = fallbackCategory.Id
    FROM BrandRequest br
    CROSS APPLY (
        SELECT TOP (1) c.Id
        FROM Category c
        WHERE c.ParentCategoryId IS NOT NULL
        ORDER BY c.DisplayOrder, c.NameAr, c.Id
    ) AS fallbackCategory
    WHERE br.CategoryId IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    IF EXISTS (SELECT 1 FROM BrandRequest WHERE CategoryId IS NULL)
        THROW 51000, 'Unable to backfill CategoryId for existing BrandRequest rows.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BrandRequest]') AND [c].[name] = N'CategoryId');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [BrandRequest] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [BrandRequest] ALTER COLUMN [CategoryId] uniqueidentifier NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    CREATE INDEX [IX_BrandRequest_CategoryId] ON [BrandRequest] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    ALTER TABLE [BrandRequest] ADD CONSTRAINT [FK_BrandRequest_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417144627_AddBrandRequestCategoryId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260417144627_AddBrandRequestCategoryId', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418084401_AddCustomerPushDevices'
)
BEGIN
    CREATE TABLE [UserPushDevices] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [DeviceToken] nvarchar(1024) NOT NULL,
        [Platform] nvarchar(20) NOT NULL,
        [DeviceId] nvarchar(200) NULL,
        [DeviceName] nvarchar(200) NULL,
        [AppVersion] nvarchar(50) NULL,
        [Locale] nvarchar(20) NULL,
        [NotificationsEnabled] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [LastRegisteredAtUtc] datetime2 NOT NULL,
        [LastSeenAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserPushDevices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPushDevices_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418084401_AddCustomerPushDevices'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserPushDevices_DeviceToken] ON [UserPushDevices] ([DeviceToken]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418084401_AddCustomerPushDevices'
)
BEGIN
    CREATE INDEX [IX_UserPushDevices_UserId_DeviceId] ON [UserPushDevices] ([UserId], [DeviceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418084401_AddCustomerPushDevices'
)
BEGIN
    CREATE INDEX [IX_UserPushDevices_UserId_IsActive] ON [UserPushDevices] ([UserId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418084401_AddCustomerPushDevices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260418084401_AddCustomerPushDevices', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418092800_AddPaymentCheckoutDeviceId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260418092800_AddPaymentCheckoutDeviceId', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418094751_SyncPaymentCheckoutDeviceIdSnapshot'
)
BEGIN
    IF COL_LENGTH('dbo.Payments', 'CheckoutDeviceId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Payments] ADD [CheckoutDeviceId] nvarchar(200) NULL;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418094751_SyncPaymentCheckoutDeviceIdSnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260418094751_SyncPaymentCheckoutDeviceIdSnapshot', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418124853_ImproveNotificationInboxPerformance'
)
BEGIN
    DROP INDEX [IX_Notifications_CreatedAtUtc] ON [Notifications];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418124853_ImproveNotificationInboxPerformance'
)
BEGIN
    DROP INDEX [IX_Notifications_UserId_IsRead] ON [Notifications];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418124853_ImproveNotificationInboxPerformance'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_CreatedAtUtc] ON [Notifications] ([UserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418124853_ImproveNotificationInboxPerformance'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead_CreatedAtUtc] ON [Notifications] ([UserId], [IsRead], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418124853_ImproveNotificationInboxPerformance'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_Type_CreatedAtUtc] ON [Notifications] ([UserId], [Type], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418124853_ImproveNotificationInboxPerformance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260418124853_ImproveNotificationInboxPerformance', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421143341_AddVendorFinanceLifecycleModeAndSettlementOrigin'
)
BEGIN
    IF COL_LENGTH('dbo.Vendor', 'FinancialLifecycleMode') IS NULL
    BEGIN
        ALTER TABLE [Vendor]
        ADD [FinancialLifecycleMode] nvarchar(50) NOT NULL DEFAULT N'Weekly';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421143341_AddVendorFinanceLifecycleModeAndSettlementOrigin'
)
BEGIN
    IF COL_LENGTH('dbo.Settlements', 'Origin') IS NULL
    BEGIN
        ALTER TABLE [Settlements]
        ADD [Origin] nvarchar(50) NOT NULL DEFAULT N'ManualBatch';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421143341_AddVendorFinanceLifecycleModeAndSettlementOrigin'
)
BEGIN
    IF COL_LENGTH('dbo.Vendor', 'FinancialLifecycleMode') IS NOT NULL
    BEGIN
        UPDATE [Vendor]
        SET [FinancialLifecycleMode] =
            CASE
                WHEN [PayoutCycle] = 'biweekly' THEN 'Biweekly'
                WHEN [PayoutCycle] = 'monthly' THEN 'Monthly'
                ELSE 'Weekly'
            END
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421143341_AddVendorFinanceLifecycleModeAndSettlementOrigin'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260421143341_AddVendorFinanceLifecycleModeAndSettlementOrigin', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422091435_AddVendorDocumentReviews'
)
BEGIN
    CREATE TABLE [VendorDocumentReviews] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [Type] nvarchar(30) NOT NULL,
        [Decision] nvarchar(20) NOT NULL,
        [RejectionReason] nvarchar(1000) NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedByName] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorDocumentReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorDocumentReviews_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422091435_AddVendorDocumentReviews'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorDocumentReviews_VendorId_Type] ON [VendorDocumentReviews] ([VendorId], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422091435_AddVendorDocumentReviews'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260422091435_AddVendorDocumentReviews', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094047_AddVendorLegalDocumentUrls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [LicenseDocumentUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094047_AddVendorLegalDocumentUrls'
)
BEGIN
    ALTER TABLE [Vendor] ADD [TaxDocumentUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422094047_AddVendorLegalDocumentUrls'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260422094047_AddVendorLegalDocumentUrls', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'VehicleImageUrl');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [Drivers] ALTER COLUMN [VehicleImageUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'PersonalPhotoUrl');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [Drivers] ALTER COLUMN [PersonalPhotoUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'NationalIdImageUrl');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [Drivers] ALTER COLUMN [NationalIdImageUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'LicenseImageUrl');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [Drivers] ALTER COLUMN [LicenseImageUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'Address');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [Drivers] ALTER COLUMN [Address] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF COL_LENGTH(N'dbo.Drivers', N'PrimaryZoneId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Drivers] ADD [PrimaryZoneId] uniqueidentifier NULL;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF COL_LENGTH(N'dbo.Drivers', N'ReviewNote') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Drivers] ADD [ReviewNote] nvarchar(500) NULL;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF COL_LENGTH(N'dbo.Drivers', N'ReviewedAtUtc') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Drivers] ADD [ReviewedAtUtc] datetime2 NULL;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF COL_LENGTH(N'dbo.Drivers', N'ReviewedByUserId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Drivers] ADD [ReviewedByUserId] uniqueidentifier NULL;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF COL_LENGTH(N'dbo.Drivers', N'SuspensionReason') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Drivers] ADD [SuspensionReason] nvarchar(500) NULL;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF COL_LENGTH(N'dbo.Drivers', N'VerificationStatus') IS NULL
    BEGIN
        ALTER TABLE [dbo].[Drivers] ADD [VerificationStatus] nvarchar(50) NULL;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[DeliveryZones]', N'U') IS NULL
    BEGIN
    CREATE TABLE [dbo].[DeliveryZones] (
        [Id] uniqueidentifier NOT NULL,
        [City] nvarchar(100) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [CenterLat] decimal(10,7) NOT NULL,
        [CenterLng] decimal(10,7) NOT NULL,
        [RadiusKm] decimal(8,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DeliveryZones] PRIMARY KEY ([Id])
    );
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[DriverIncidents]', N'U') IS NULL
    BEGIN
    CREATE TABLE [dbo].[DriverIncidents] (
        [Id] uniqueidentifier NOT NULL,
        [DriverId] uniqueidentifier NOT NULL,
        [IncidentType] nvarchar(200) NOT NULL,
        [Severity] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [ReviewerName] nvarchar(200) NULL,
        [LinkedOrderId] uniqueidentifier NULL,
        [Summary] nvarchar(1000) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DriverIncidents] PRIMARY KEY ([Id])
    );
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[DriverNotes]', N'U') IS NULL
    BEGIN
    CREATE TABLE [dbo].[DriverNotes] (
        [Id] uniqueidentifier NOT NULL,
        [DriverId] uniqueidentifier NOT NULL,
        [AuthorUserId] uniqueidentifier NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DriverNotes] PRIMARY KEY ([Id])
    );
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Drivers_PrimaryZoneId'
          AND object_id = OBJECT_ID(N'[dbo].[Drivers]')
    )
    BEGIN
        CREATE INDEX [IX_Drivers_PrimaryZoneId] ON [dbo].[Drivers] ([PrimaryZoneId]);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_DriverIncidents_DriverId'
          AND object_id = OBJECT_ID(N'[dbo].[DriverIncidents]')
    )
    BEGIN
        CREATE INDEX [IX_DriverIncidents_DriverId] ON [dbo].[DriverIncidents] ([DriverId]);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_DriverNotes_AuthorUserId'
          AND object_id = OBJECT_ID(N'[dbo].[DriverNotes]')
    )
    BEGIN
        CREATE INDEX [IX_DriverNotes_AuthorUserId] ON [dbo].[DriverNotes] ([AuthorUserId]);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_DriverNotes_DriverId'
          AND object_id = OBJECT_ID(N'[dbo].[DriverNotes]')
    )
    BEGIN
        CREATE INDEX [IX_DriverNotes_DriverId] ON [dbo].[DriverNotes] ([DriverId]);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    UPDATE [Drivers]
    SET [VerificationStatus] = CASE
        WHEN [Status] IN ('Active', 'Suspended') THEN 'Approved'
        WHEN NULLIF(LTRIM(RTRIM([NationalIdImageUrl])), '') IS NOT NULL
         AND NULLIF(LTRIM(RTRIM([LicenseImageUrl])), '') IS NOT NULL
         AND NULLIF(LTRIM(RTRIM([VehicleImageUrl])), '') IS NOT NULL
         AND NULLIF(LTRIM(RTRIM([PersonalPhotoUrl])), '') IS NOT NULL
            THEN 'UnderReview'
        ELSE 'NeedsDocuments'
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'VerificationStatus');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [Drivers] ALTER COLUMN [VerificationStatus] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_DriverIncidents_Drivers_DriverId'
    )
    BEGIN
        ALTER TABLE [dbo].[DriverIncidents] ADD CONSTRAINT [FK_DriverIncidents_Drivers_DriverId]
            FOREIGN KEY ([DriverId])
            REFERENCES [dbo].[Drivers] ([Id])
            ON DELETE CASCADE;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_DriverNotes_AspNetUsers_AuthorUserId'
    )
    BEGIN
        ALTER TABLE [dbo].[DriverNotes] ADD CONSTRAINT [FK_DriverNotes_AspNetUsers_AuthorUserId]
            FOREIGN KEY ([AuthorUserId])
            REFERENCES [dbo].[AspNetUsers] ([Id])
            ON DELETE NO ACTION;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_DriverNotes_Drivers_DriverId'
    )
    BEGIN
        ALTER TABLE [dbo].[DriverNotes] ADD CONSTRAINT [FK_DriverNotes_Drivers_DriverId]
            FOREIGN KEY ([DriverId])
            REFERENCES [dbo].[Drivers] ([Id])
            ON DELETE CASCADE;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_Drivers_DeliveryZones_PrimaryZoneId'
    )
    BEGIN
        ALTER TABLE [dbo].[Drivers] ADD CONSTRAINT [FK_Drivers_DeliveryZones_PrimaryZoneId]
            FOREIGN KEY ([PrimaryZoneId])
            REFERENCES [dbo].[DeliveryZones] ([Id])
            ON DELETE SET NULL;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423111935_AddDriverDispatchAndReviewWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260423111935_AddDriverDispatchAndReviewWorkflow', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423165401_AddDriverLocationAccuracyMeters'
)
BEGIN
    ALTER TABLE [DriverLocations] ADD [AccuracyMeters] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423165401_AddDriverLocationAccuracyMeters'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260423165401_AddDriverLocationAccuracyMeters', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423184500_NormalizeLegacyDriverVehicleTypes'
)
BEGIN
    UPDATE [Drivers]
    SET [VehicleType] = CASE
        WHEN [VehicleType] IS NULL THEN NULL
        WHEN LOWER(LTRIM(RTRIM([VehicleType]))) = 'bike' THEN 'Bicycle'
        WHEN LOWER(LTRIM(RTRIM([VehicleType]))) = 'cargo van' THEN 'Van'
        WHEN LOWER(LTRIM(RTRIM([VehicleType]))) = 'motorbike' THEN 'Motorcycle'
        ELSE [VehicleType]
    END
    WHERE [VehicleType] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260423184500_NormalizeLegacyDriverVehicleTypes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260423184500_NormalizeLegacyDriverVehicleTypes', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260424150633_AddVendorReviewReplyFields'
)
BEGIN
    ALTER TABLE [Reviews] ADD [VendorRepliedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260424150633_AddVendorReviewReplyFields'
)
BEGIN
    ALTER TABLE [Reviews] ADD [VendorReply] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260424150633_AddVendorReviewReplyFields'
)
BEGIN
    ALTER TABLE [Reviews] ADD [VendorReplyUpdatedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260424150633_AddVendorReviewReplyFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260424150633_AddVendorReviewReplyFields', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260424151117_AddVendorWorkspaceStates'
)
BEGIN
    CREATE TABLE [VendorWorkspaceStates] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [Feature] nvarchar(80) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorWorkspaceStates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorWorkspaceStates_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260424151117_AddVendorWorkspaceStates'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorWorkspaceStates_VendorId_Feature] ON [VendorWorkspaceStates] ([VendorId], [Feature]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260424151117_AddVendorWorkspaceStates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260424151117_AddVendorWorkspaceStates', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Orders] ADD [BaseDeliveryFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Orders] ADD [DeliveryPricingMode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Orders] ADD [DeliveryPricingRuleLabel] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Orders] ADD [DistanceDeliveryFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Orders] ADD [QuotedDistanceKm] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Orders] ADD [SurgeDeliveryFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [DispatchAttemptNumber] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [OfferExpiresAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [OfferRejectedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [OfferRejectedReason] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Carts] ADD [BaseDeliveryFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Carts] ADD [DeliveryPricingMode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Carts] ADD [DeliveryPricingRuleLabel] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Carts] ADD [DistanceDeliveryFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Carts] ADD [QuotedDistanceKm] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    ALTER TABLE [Carts] ADD [SurgeDeliveryFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    CREATE TABLE [DeliveryOfferAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [AssignmentId] uniqueidentifier NULL,
        [DriverId] uniqueidentifier NOT NULL,
        [AttemptNumber] int NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [OfferedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [RespondedAtUtc] datetime2 NULL,
        [RejectionReason] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DeliveryOfferAttempts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    CREATE TABLE [DeliveryPricingRules] (
        [Id] uniqueidentifier NOT NULL,
        [DeliveryZoneId] uniqueidentifier NULL,
        [City] nvarchar(100) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [BaseFee] decimal(18,2) NOT NULL,
        [IncludedKm] decimal(18,2) NOT NULL,
        [PerKmFee] decimal(18,2) NOT NULL,
        [MinFee] decimal(18,2) NOT NULL,
        [MaxFee] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DeliveryPricingRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeliveryPricingRules_DeliveryZones_DeliveryZoneId] FOREIGN KEY ([DeliveryZoneId]) REFERENCES [DeliveryZones] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    CREATE TABLE [DeliveryPricingSurgeWindows] (
        [Id] uniqueidentifier NOT NULL,
        [DeliveryPricingRuleId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [StartLocalTime] time NOT NULL,
        [EndLocalTime] time NOT NULL,
        [Multiplier] decimal(8,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DeliveryPricingSurgeWindows] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DeliveryPricingSurgeWindows_DeliveryPricingRules_DeliveryPricingRuleId] FOREIGN KEY ([DeliveryPricingRuleId]) REFERENCES [DeliveryPricingRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    CREATE INDEX [IX_DeliveryOfferAttempts_OrderId_AttemptNumber] ON [DeliveryOfferAttempts] ([OrderId], [AttemptNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    CREATE INDEX [IX_DeliveryOfferAttempts_OrderId_DriverId_Status] ON [DeliveryOfferAttempts] ([OrderId], [DriverId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    CREATE INDEX [IX_DeliveryPricingRules_City_IsActive] ON [DeliveryPricingRules] ([City], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    CREATE INDEX [IX_DeliveryPricingRules_DeliveryZoneId_IsActive] ON [DeliveryPricingRules] ([DeliveryZoneId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    CREATE INDEX [IX_DeliveryPricingSurgeWindows_DeliveryPricingRuleId_IsActive] ON [DeliveryPricingSurgeWindows] ([DeliveryPricingRuleId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425080112_AddDriverOfferEngineAndDeliveryPricing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260425080112_AddDriverOfferEngineAndDeliveryPricing', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425082741_AddPickupAndDeliveryOtpWorkflow'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [DeliveryOtpCode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425082741_AddPickupAndDeliveryOtpWorkflow'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [DeliveryOtpExpiresAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425082741_AddPickupAndDeliveryOtpWorkflow'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [DeliveryOtpVerifiedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425082741_AddPickupAndDeliveryOtpWorkflow'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [DeliveryOtpVerifiedByDriverId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425082741_AddPickupAndDeliveryOtpWorkflow'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [PickupOtpCode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425082741_AddPickupAndDeliveryOtpWorkflow'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [PickupOtpExpiresAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425082741_AddPickupAndDeliveryOtpWorkflow'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [PickupOtpVerifiedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425082741_AddPickupAndDeliveryOtpWorkflow'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [PickupOtpVerifiedByDriverId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425082741_AddPickupAndDeliveryOtpWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260425082741_AddPickupAndDeliveryOtpWorkflow', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425083953_AddDriverArrivalStateTimestamps'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [ArrivedAtCustomerAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425083953_AddDriverArrivalStateTimestamps'
)
BEGIN
    ALTER TABLE [DeliveryAssignments] ADD [ArrivedAtVendorAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425083953_AddDriverArrivalStateTimestamps'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260425083953_AddDriverArrivalStateTimestamps', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425094134_AddDriverWalletAndDriverMobileApis'
)
BEGIN
    CREATE TABLE [DriverPayoutMethods] (
        [Id] uniqueidentifier NOT NULL,
        [DriverId] uniqueidentifier NOT NULL,
        [MethodType] nvarchar(50) NOT NULL DEFAULT N'BankAccount',
        [AccountHolderName] nvarchar(200) NOT NULL,
        [ProviderName] nvarchar(200) NULL,
        [AccountIdentifier] nvarchar(100) NOT NULL,
        [MaskedLabel] nvarchar(250) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [IsVerified] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DriverPayoutMethods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425094134_AddDriverWalletAndDriverMobileApis'
)
BEGIN
    CREATE TABLE [DriverWithdrawalRequests] (
        [Id] uniqueidentifier NOT NULL,
        [DriverId] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NOT NULL,
        [DriverPayoutMethodId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL DEFAULT N'Pending',
        [TransferReference] nvarchar(200) NULL,
        [FailureReason] nvarchar(500) NULL,
        [ProcessedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DriverWithdrawalRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DriverWithdrawalRequests_DriverPayoutMethods_DriverPayoutMethodId] FOREIGN KEY ([DriverPayoutMethodId]) REFERENCES [DriverPayoutMethods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DriverWithdrawalRequests_Wallet_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [Wallet] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425094134_AddDriverWalletAndDriverMobileApis'
)
BEGIN
    CREATE INDEX [IX_DriverPayoutMethods_DriverId] ON [DriverPayoutMethods] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425094134_AddDriverWalletAndDriverMobileApis'
)
BEGIN
    CREATE INDEX [IX_DriverWithdrawalRequests_DriverId] ON [DriverWithdrawalRequests] ([DriverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425094134_AddDriverWalletAndDriverMobileApis'
)
BEGIN
    CREATE INDEX [IX_DriverWithdrawalRequests_DriverPayoutMethodId] ON [DriverWithdrawalRequests] ([DriverPayoutMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425094134_AddDriverWalletAndDriverMobileApis'
)
BEGIN
    CREATE INDEX [IX_DriverWithdrawalRequests_WalletId] ON [DriverWithdrawalRequests] ([WalletId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260425094134_AddDriverWalletAndDriverMobileApis'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260425094134_AddDriverWalletAndDriverMobileApis', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427121835_AddSaudiGeography'
)
BEGIN
    CREATE TABLE [SaudiRegions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NOT NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [MapZoom] int NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SaudiRegions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427121835_AddSaudiGeography'
)
BEGIN
    CREATE TABLE [SaudiCities] (
        [Id] uniqueidentifier NOT NULL,
        [RegionId] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [NameAr] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(100) NOT NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [MapZoom] int NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SaudiCities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SaudiCities_SaudiRegions_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [SaudiRegions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427121835_AddSaudiGeography'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SaudiCities_Code] ON [SaudiCities] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427121835_AddSaudiGeography'
)
BEGIN
    CREATE INDEX [IX_SaudiCities_RegionId] ON [SaudiCities] ([RegionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427121835_AddSaudiGeography'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SaudiRegions_Code] ON [SaudiRegions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427121835_AddSaudiGeography'
)
BEGIN

    INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc]) VALUES
    ('10000000-0000-0000-0000-000000000001', N'RIYADH', N'منطقة الرياض', N'Riyadh Region', 24.7136, 46.6753, 8, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000002', N'MAKKAH', N'منطقة مكة المكرمة', N'Makkah Region', 21.4225, 39.8262, 8, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000003', N'MADINAH', N'منطقة المدينة المنورة', N'Madinah Region', 24.4672, 39.6024, 8, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000004', N'EASTERN', N'المنطقة الشرقية', N'Eastern Region', 26.3927, 49.9777, 7, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000005', N'QASSIM', N'منطقة القصيم', N'Qassim Region', 26.3267, 43.9650, 8, 5, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000006', N'HAIL', N'منطقة حائل', N'Hail Region', 27.5114, 41.7208, 8, 6, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000007', N'TABUK', N'منطقة تبوك', N'Tabuk Region', 28.3835, 36.5662, 7, 7, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000008', N'NORTHERN_BORDERS', N'منطقة الحدود الشمالية', N'Northern Borders', 30.9753, 41.0186, 7, 8, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000009', N'JAWF', N'منطقة الجوف', N'Al Jawf Region', 29.8868, 39.3206, 8, 9, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000010', N'JIZAN', N'منطقة جازان', N'Jizan Region', 16.8893, 42.5510, 9, 10, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000011', N'ASIR', N'منطقة عسير', N'Asir Region', 18.2164, 42.5053, 8, 11, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000012', N'BAHA', N'منطقة الباحة', N'Al Baha Region', 20.0000, 41.4667, 9, 12, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('10000000-0000-0000-0000-000000000013', N'NAJRAN', N'منطقة نجران', N'Najran Region', 17.4933, 44.1322, 8, 13, SYSUTCDATETIME(), SYSUTCDATETIME());

    INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc]) VALUES
    ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', N'RIYADH', N'الرياض', N'Riyadh', 24.7136, 46.6753, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', N'KHARJ', N'الخرج', N'Al Kharj', 24.1500, 47.3000, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', N'DAWADMI', N'الدوادمي', N'Ad Dawadmi', 24.5000, 44.3833, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000001', N'MAJMAAH', N'المجمعة', N'Al Majma''ah', 25.9000, 45.3500, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000001', N'WADI_DAWASIR', N'وادي الدواسر', N'Wadi ad-Dawasir', 20.4500, 44.7833, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000006', '10000000-0000-0000-0000-000000000001', N'AFIF', N'عفيف', N'Afif', 23.9167, 42.9333, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000007', '10000000-0000-0000-0000-000000000001', N'SHAQRA', N'شقراء', N'Shaqra', 25.2500, 45.2500, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000008', '10000000-0000-0000-0000-000000000002', N'MAKKAH', N'مكة المكرمة', N'Makkah', 21.4225, 39.8262, 13, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000009', '10000000-0000-0000-0000-000000000002', N'JEDDAH', N'جدة', N'Jeddah', 21.5433, 39.1728, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000010', '10000000-0000-0000-0000-000000000002', N'TAIF', N'الطائف', N'Taif', 21.2703, 40.4159, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000011', '10000000-0000-0000-0000-000000000002', N'RABIGH', N'رابغ', N'Rabigh', 22.7985, 39.0350, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000012', '10000000-0000-0000-0000-000000000002', N'QUNFUDHAH', N'القنفذة', N'Al Qunfudhah', 19.1269, 41.0789, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000013', '10000000-0000-0000-0000-000000000003', N'MADINAH', N'المدينة المنورة', N'Madinah', 24.4672, 39.6024, 13, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000014', '10000000-0000-0000-0000-000000000003', N'YANBU', N'ينبع', N'Yanbu', 24.0886, 38.0633, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000015', '10000000-0000-0000-0000-000000000003', N'ULA', N'العلا', N'Al Ula', 26.6096, 37.9200, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000016', '10000000-0000-0000-0000-000000000003', N'BADR', N'بدر', N'Badr', 23.7831, 38.7885, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000017', '10000000-0000-0000-0000-000000000004', N'DAMMAM', N'الدمام', N'Dammam', 26.3927, 49.9777, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000018', '10000000-0000-0000-0000-000000000004', N'KHOBAR', N'الخبر', N'Al Khobar', 26.2172, 50.1971, 13, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000019', '10000000-0000-0000-0000-000000000004', N'DHAHRAN', N'الظهران', N'Dhahran', 26.2361, 50.0393, 13, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000020', '10000000-0000-0000-0000-000000000004', N'JUBAIL', N'الجبيل', N'Jubail', 27.0046, 49.6226, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000021', '10000000-0000-0000-0000-000000000004', N'QATIF', N'القطيف', N'Qatif', 26.5240, 50.0134, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000022', '10000000-0000-0000-0000-000000000004', N'HOFUF', N'الهفوف', N'Al Hofuf', 25.3809, 49.5866, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000023', '10000000-0000-0000-0000-000000000004', N'MUBARRAZ', N'المبرز', N'Al Mubarraz', 25.4282, 49.5614, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000024', '10000000-0000-0000-0000-000000000004', N'KHAFJI', N'الخفجي', N'Khafji', 28.4392, 48.4926, 12, 8, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000025', '10000000-0000-0000-0000-000000000005', N'BURAYDAH', N'بريدة', N'Buraydah', 26.3267, 43.9650, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000026', '10000000-0000-0000-0000-000000000005', N'UNAYZAH', N'عنيزة', N'Unayzah', 26.0842, 43.9887, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000027', '10000000-0000-0000-0000-000000000005', N'RASS', N'الرس', N'Ar Rass', 25.8523, 43.4946, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000028', '10000000-0000-0000-0000-000000000006', N'HAIL_CITY', N'حائل', N'Hail', 27.5114, 41.7208, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000029', '10000000-0000-0000-0000-000000000006', N'BAQAA', N'بقعاء', N'Baqa''a', 27.9000, 42.3833, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000030', '10000000-0000-0000-0000-000000000007', N'TABUK_CITY', N'تبوك', N'Tabuk', 28.3835, 36.5662, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000031', '10000000-0000-0000-0000-000000000007', N'WAJH', N'الوجه', N'Al Wajh', 26.2310, 36.4541, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000032', '10000000-0000-0000-0000-000000000007', N'DUBA', N'ضباء', N'Duba', 27.3491, 35.6987, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000033', '10000000-0000-0000-0000-000000000007', N'NEOM', N'نيوم', N'NEOM', 28.0000, 35.0000, 10, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000034', '10000000-0000-0000-0000-000000000008', N'ARAR', N'عرعر', N'Arar', 30.9753, 41.0186, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000035', '10000000-0000-0000-0000-000000000008', N'RAFHA', N'رفحاء', N'Rafha', 29.6208, 43.4932, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000036', '10000000-0000-0000-0000-000000000008', N'TURAIF', N'طريف', N'Turaif', 31.6716, 38.6554, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000037', '10000000-0000-0000-0000-000000000009', N'SAKAKA', N'سكاكا', N'Sakaka', 29.9697, 40.2064, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000038', '10000000-0000-0000-0000-000000000009', N'DUMAT_JANDAL', N'دومة الجندل', N'Dumat Al-Jandal', 29.8136, 39.8618, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000039', '10000000-0000-0000-0000-000000000009', N'QURAYAT', N'القريات', N'Qurayat', 31.3343, 37.3428, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000040', '10000000-0000-0000-0000-000000000010', N'JIZAN_CITY', N'جازان', N'Jizan', 16.8893, 42.5510, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000041', '10000000-0000-0000-0000-000000000010', N'SABYA', N'صبيا', N'Sabya', 17.1509, 42.6231, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000042', '10000000-0000-0000-0000-000000000010', N'ABU_ARISH', N'أبو عريش', N'Abu Arish', 16.9618, 42.8304, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000043', '10000000-0000-0000-0000-000000000011', N'ABHA', N'أبها', N'Abha', 18.2164, 42.5053, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000044', '10000000-0000-0000-0000-000000000011', N'KHAMIS_MUSHAIT', N'خميس مشيط', N'Khamis Mushait', 18.3000, 42.7333, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000045', '10000000-0000-0000-0000-000000000011', N'BISHA', N'بيشة', N'Bisha', 19.9833, 42.6000, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000046', '10000000-0000-0000-0000-000000000011', N'NAMAS', N'النماص', N'An Namas', 19.1189, 42.1304, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000047', '10000000-0000-0000-0000-000000000012', N'BAHA_CITY', N'الباحة', N'Al Baha', 20.0000, 41.4667, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000048', '10000000-0000-0000-0000-000000000012', N'BALJURASHI', N'بلجرشي', N'Baljurashi', 19.8500, 41.6167, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000049', '10000000-0000-0000-0000-000000000013', N'NAJRAN_CITY', N'نجران', N'Najran', 17.4933, 44.1322, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
    ('20000000-0000-0000-0000-000000000050', '10000000-0000-0000-0000-000000000013', N'SHARURAH', N'شرورة', N'Sharurah', 17.4875, 47.1128, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME());

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427121835_AddSaudiGeography'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260427121835_AddSaudiGeography', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427163203_AddDriverRegionCity'
)
BEGIN
    ALTER TABLE [Drivers] ADD [City] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427163203_AddDriverRegionCity'
)
BEGIN
    ALTER TABLE [Drivers] ADD [Region] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427163203_AddDriverRegionCity'
)
BEGIN
    CREATE INDEX [IX_Drivers_City_Status] ON [Drivers] ([City], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427163203_AddDriverRegionCity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260427163203_AddDriverRegionCity', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427172026_SplitNationalIdFrontBack'
)
BEGIN
    EXEC sp_rename N'[Drivers].[NationalIdImageUrl]', N'NationalIdFrontImageUrl', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427172026_SplitNationalIdFrontBack'
)
BEGIN
    ALTER TABLE [Drivers] ADD [NationalIdBackImageUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427172026_SplitNationalIdFrontBack'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260427172026_SplitNationalIdFrontBack', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427183743_RemoveDriverPrimaryZone'
)
BEGIN
    ALTER TABLE [Drivers] DROP CONSTRAINT [FK_Drivers_DeliveryZones_PrimaryZoneId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427183743_RemoveDriverPrimaryZone'
)
BEGIN
    DROP INDEX [IX_Drivers_PrimaryZoneId] ON [Drivers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427183743_RemoveDriverPrimaryZone'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Drivers]') AND [c].[name] = N'PrimaryZoneId');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [Drivers] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [Drivers] DROP COLUMN [PrimaryZoneId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427183743_RemoveDriverPrimaryZone'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260427183743_RemoveDriverPrimaryZone', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    ALTER TABLE [Refunds] ADD [CostBearer] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    ALTER TABLE [Refunds] ADD [OrderSupportCaseId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    ALTER TABLE [Refunds] ADD [RefundMethod] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    CREATE TABLE [OrderSupportCases] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [CustomerUserId] uniqueidentifier NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [Priority] nvarchar(50) NOT NULL,
        [Queue] nvarchar(50) NOT NULL,
        [AssignedAdminId] uniqueidentifier NULL,
        [AssignedAtUtc] datetime2 NULL,
        [SlaDueAtUtc] datetime2 NULL,
        [ReasonCode] nvarchar(100) NULL,
        [Message] nvarchar(2000) NOT NULL,
        [DecisionNotes] nvarchar(2000) NULL,
        [CustomerVisibleNote] nvarchar(2000) NULL,
        [RequestedRefundAmount] decimal(18,2) NULL,
        [ApprovedRefundAmount] decimal(18,2) NULL,
        [RefundMethod] nvarchar(50) NULL,
        [CostBearer] nvarchar(50) NULL,
        [ClosedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderSupportCases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderSupportCases_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    CREATE TABLE [OrderSupportCaseActivities] (
        [Id] uniqueidentifier NOT NULL,
        [OrderSupportCaseId] uniqueidentifier NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Note] nvarchar(2000) NULL,
        [ActorUserId] uniqueidentifier NULL,
        [ActorRole] nvarchar(50) NOT NULL,
        [VisibleToCustomer] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderSupportCaseActivities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderSupportCaseActivities_OrderSupportCases_OrderSupportCaseId] FOREIGN KEY ([OrderSupportCaseId]) REFERENCES [OrderSupportCases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    CREATE TABLE [OrderSupportCaseAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [OrderSupportCaseId] uniqueidentifier NOT NULL,
        [UploadedByUserId] uniqueidentifier NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FileUrl] nvarchar(2000) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderSupportCaseAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderSupportCaseAttachments_OrderSupportCases_OrderSupportCaseId] FOREIGN KEY ([OrderSupportCaseId]) REFERENCES [OrderSupportCases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    CREATE INDEX [IX_OrderSupportCaseActivities_OrderSupportCaseId] ON [OrderSupportCaseActivities] ([OrderSupportCaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    CREATE INDEX [IX_OrderSupportCaseAttachments_OrderSupportCaseId] ON [OrderSupportCaseAttachments] ([OrderSupportCaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    CREATE INDEX [IX_OrderSupportCases_OrderId_Status] ON [OrderSupportCases] ([OrderId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430091742_AddOrderSupportCases'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260430091742_AddOrderSupportCases', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430113000_AddDriverLocationUpdateAccessControl'
)
BEGIN
    ALTER TABLE [Drivers] ADD [LocationUpdatesBlockReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430113000_AddDriverLocationUpdateAccessControl'
)
BEGIN
    ALTER TABLE [Drivers] ADD [LocationUpdatesBlockedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430113000_AddDriverLocationUpdateAccessControl'
)
BEGIN
    ALTER TABLE [Drivers] ADD [LocationUpdatesBlockedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430113000_AddDriverLocationUpdateAccessControl'
)
BEGIN
    ALTER TABLE [Drivers] ADD [IsLocationUpdatesBlocked] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260430113000_AddDriverLocationUpdateAccessControl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260430113000_AddDriverLocationUpdateAccessControl', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501102209_AddMultiStakeholderSupportFields'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [DriverRespondedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501102209_AddMultiStakeholderSupportFields'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [DriverResponse] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501102209_AddMultiStakeholderSupportFields'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [InitiatorRole] nvarchar(20) NOT NULL DEFAULT N'customer';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501102209_AddMultiStakeholderSupportFields'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [ResolutionCode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501102209_AddMultiStakeholderSupportFields'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [VendorRespondedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501102209_AddMultiStakeholderSupportFields'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [VendorResponse] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260501102209_AddMultiStakeholderSupportFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260501102209_AddMultiStakeholderSupportFields', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502132352_AddWalletTransactionIdToSettlementItems'
)
BEGIN
    ALTER TABLE [SettlementItems] ADD [WalletTransactionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502132352_AddWalletTransactionIdToSettlementItems'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260502132352_AddWalletTransactionIdToSettlementItems', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502192743_AddUnifiedSupportCaseConversationTimeline'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [AwaitingResponseFromRole] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502192743_AddUnifiedSupportCaseConversationTimeline'
)
BEGIN
    ALTER TABLE [OrderSupportCaseActivities] ADD [Audience] nvarchar(100) NOT NULL DEFAULT N'all_external';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502192743_AddUnifiedSupportCaseConversationTimeline'
)
BEGIN
    ALTER TABLE [OrderSupportCaseActivities] ADD [IsInternalOnly] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502192743_AddUnifiedSupportCaseConversationTimeline'
)
BEGIN
    ALTER TABLE [OrderSupportCaseActivities] ADD [MessageType] nvarchar(50) NOT NULL DEFAULT N'system';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260502192743_AddUnifiedSupportCaseConversationTimeline'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260502192743_AddUnifiedSupportCaseConversationTimeline', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503105122_AddZoneFinanceSettings'
)
BEGIN
    CREATE TABLE [ZoneFinanceSettings] (
        [Id] uniqueidentifier NOT NULL,
        [DeliveryZoneId] uniqueidentifier NOT NULL,
        [VatPercent] decimal(18,2) NOT NULL,
        [CodFeeType] nvarchar(20) NOT NULL,
        [CodFlatFee] decimal(18,2) NOT NULL,
        [CodPercent] decimal(18,2) NOT NULL,
        [IsVatActive] bit NOT NULL,
        [IsCodFeeActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ZoneFinanceSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503105122_AddZoneFinanceSettings'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ZoneFinanceSettings_DeliveryZoneId] ON [ZoneFinanceSettings] ([DeliveryZoneId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503105122_AddZoneFinanceSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260503105122_AddZoneFinanceSettings', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503110816_AddVatAndCodToOrder'
)
BEGIN
    ALTER TABLE [Orders] ADD [CodFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503110816_AddVatAndCodToOrder'
)
BEGIN
    ALTER TABLE [Orders] ADD [VatAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503110816_AddVatAndCodToOrder'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260503110816_AddVatAndCodToOrder', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PermissionVersion] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE TABLE [PermissionDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(150) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Domain] nvarchar(100) NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [PanelScope] nvarchar(50) NOT NULL,
        [IsSensitive] bit NOT NULL,
        CONSTRAINT [PK_PermissionDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE TABLE [RoleDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsSystem] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [IdentityRole] nvarchar(20) NOT NULL,
        [PanelScope] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_RoleDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE TABLE [UserPermissionOverrides] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PermissionKey] nvarchar(150) NOT NULL,
        [Mode] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserPermissionOverrides] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPermissionOverrides_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [RoleDefinitionId] uniqueidentifier NOT NULL,
        [PermissionDefinitionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleDefinitionId], [PermissionDefinitionId]),
        CONSTRAINT [FK_RolePermissions_PermissionDefinitions_PermissionDefinitionId] FOREIGN KEY ([PermissionDefinitionId]) REFERENCES [PermissionDefinitions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_RoleDefinitions_RoleDefinitionId] FOREIGN KEY ([RoleDefinitionId]) REFERENCES [RoleDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE TABLE [UserAccessScopes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleDefinitionId] uniqueidentifier NOT NULL,
        [PanelScope] nvarchar(50) NOT NULL,
        [ScopeType] nvarchar(50) NOT NULL,
        [ScopeEntityId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [GrantedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserAccessScopes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserAccessScopes_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserAccessScopes_RoleDefinitions_RoleDefinitionId] FOREIGN KEY ([RoleDefinitionId]) REFERENCES [RoleDefinitions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PermissionDefinitions_Key] ON [PermissionDefinitions] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoleDefinitions_Code] ON [RoleDefinitions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionDefinitionId] ON [RolePermissions] ([PermissionDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE INDEX [IX_UserAccessScopes_RoleDefinitionId] ON [UserAccessScopes] ([RoleDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE INDEX [IX_UserAccessScopes_UserId_IsActive] ON [UserAccessScopes] ([UserId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    CREATE INDEX [IX_UserPermissionOverrides_UserId_PermissionKey_IsActive] ON [UserPermissionOverrides] ([UserId], [PermissionKey], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504084330_AddAccessControlFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504084330_AddAccessControlFoundation', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144903_AddSupportCompensationCoupons'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [CompensationCouponId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144903_AddSupportCompensationCoupons'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [CompensationType] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144903_AddSupportCompensationCoupons'
)
BEGIN
    ALTER TABLE [Coupons] ADD [AssignedUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144903_AddSupportCompensationCoupons'
)
BEGIN
    ALTER TABLE [Coupons] ADD [OrderSupportCaseId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144903_AddSupportCompensationCoupons'
)
BEGIN
    ALTER TABLE [Coupons] ADD [SourceType] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505144903_AddSupportCompensationCoupons'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505144903_AddSupportCompensationCoupons', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505153759_AddVendorRecoveryWorkflow'
)
BEGIN
    CREATE TABLE [VendorRecoveries] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [OrderSupportCaseId] uniqueidentifier NOT NULL,
        [TargetAmount] decimal(18,2) NOT NULL,
        [RecoveredAmount] decimal(18,2) NOT NULL,
        [OutstandingAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [Source] nvarchar(50) NULL,
        [SettlementId] uniqueidentifier NULL,
        [PayoutId] uniqueidentifier NULL,
        [WalletTransactionId] uniqueidentifier NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorRecoveries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505153759_AddVendorRecoveryWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorRecoveries_OrderSupportCaseId] ON [VendorRecoveries] ([OrderSupportCaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505153759_AddVendorRecoveryWorkflow'
)
BEGIN
    CREATE INDEX [IX_VendorRecoveries_VendorId_Status] ON [VendorRecoveries] ([VendorId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505153759_AddVendorRecoveryWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505153759_AddVendorRecoveryWorkflow', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AccountPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AssignmentPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [DispatchPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [SupportPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [WalletPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    ALTER TABLE [Notifications] ADD [Category] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    ALTER TABLE [Notifications] ADD [Priority] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_Category_CreatedAtUtc] ON [Notifications] ([UserId], [Category], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_Priority_CreatedAtUtc] ON [Notifications] ([UserId], [Priority], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506123748_AddDriverRealtimeNotificationProfiles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260506123748_AddDriverRealtimeNotificationProfiles', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507114156_AddEmailCenter'
)
BEGIN
    CREATE TABLE [EmailDispatchLogs] (
        [Id] uniqueidentifier NOT NULL,
        [RuleKey] nvarchar(100) NULL,
        [RuleLabel] nvarchar(200) NOT NULL,
        [AudienceType] nvarchar(50) NOT NULL,
        [Source] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [Subject] nvarchar(500) NOT NULL,
        [ToRecipientsJson] nvarchar(max) NOT NULL,
        [CcRecipientsJson] nvarchar(max) NOT NULL,
        [BccRecipientsJson] nvarchar(max) NOT NULL,
        [Provider] nvarchar(50) NULL,
        [ProviderMessageId] nvarchar(200) NULL,
        [FailureReason] nvarchar(2000) NULL,
        [EventKey] nvarchar(100) NULL,
        [TriggeredByUserId] uniqueidentifier NULL,
        [EntityId] uniqueidentifier NULL,
        [VendorId] uniqueidentifier NULL,
        [BranchId] uniqueidentifier NULL,
        [IsTestSend] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailDispatchLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507114156_AddEmailCenter'
)
BEGIN
    CREATE TABLE [EmailSenderProfileConfigs] (
        [Id] uniqueidentifier NOT NULL,
        [ProfileKey] nvarchar(100) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Address] nvarchar(256) NOT NULL,
        [ReplyTo] nvarchar(256) NOT NULL,
        [DescriptionKey] nvarchar(200) NOT NULL,
        [Locale] nvarchar(50) NOT NULL,
        [IsDefault] bit NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [IsReadOnly] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailSenderProfileConfigs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507114156_AddEmailCenter'
)
BEGIN
    CREATE TABLE [EmailWorkflowRuleConfigs] (
        [Id] uniqueidentifier NOT NULL,
        [RuleKey] nvarchar(100) NOT NULL,
        [TitleKey] nvarchar(200) NOT NULL,
        [SubtitleKey] nvarchar(300) NOT NULL,
        [CategoryKey] nvarchar(150) NOT NULL,
        [CadenceLabelKey] nvarchar(150) NOT NULL,
        [TriggerNotesKey] nvarchar(400) NOT NULL,
        [Enabled] bit NOT NULL,
        [SenderProfileKey] nvarchar(100) NOT NULL,
        [AudienceType] nvarchar(50) NOT NULL,
        [PanelScope] nvarchar(50) NOT NULL,
        [PersonaTargetsJson] nvarchar(max) NOT NULL,
        [EntityScopeJson] nvarchar(max) NOT NULL,
        [BranchScopeMode] nvarchar(50) NOT NULL,
        [RecipientTargetsJson] nvarchar(max) NOT NULL,
        [RouteJson] nvarchar(max) NOT NULL,
        [TemplateJson] nvarchar(max) NOT NULL,
        [AutomationState] nvarchar(50) NOT NULL,
        [EventKey] nvarchar(100) NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailWorkflowRuleConfigs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507114156_AddEmailCenter'
)
BEGIN
    CREATE INDEX [IX_EmailDispatchLogs_RuleKey_CreatedAtUtc] ON [EmailDispatchLogs] ([RuleKey], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507114156_AddEmailCenter'
)
BEGIN
    CREATE INDEX [IX_EmailDispatchLogs_Source_Status_CreatedAtUtc] ON [EmailDispatchLogs] ([Source], [Status], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507114156_AddEmailCenter'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmailSenderProfileConfigs_ProfileKey] ON [EmailSenderProfileConfigs] ([ProfileKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507114156_AddEmailCenter'
)
BEGIN
    CREATE INDEX [IX_EmailWorkflowRuleConfigs_EventKey] ON [EmailWorkflowRuleConfigs] ([EventKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507114156_AddEmailCenter'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmailWorkflowRuleConfigs_RuleKey] ON [EmailWorkflowRuleConfigs] ([RuleKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507114156_AddEmailCenter'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260507114156_AddEmailCenter', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507140035_AddDriverDocumentComplianceExpansion'
)
BEGIN
    ALTER TABLE [Drivers] ADD [DriverLicenseExpiryDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507140035_AddDriverDocumentComplianceExpansion'
)
BEGIN
    ALTER TABLE [Drivers] ADD [NationalIdExpiryDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507140035_AddDriverDocumentComplianceExpansion'
)
BEGIN
    ALTER TABLE [Drivers] ADD [VehicleLicenseExpiryDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507140035_AddDriverDocumentComplianceExpansion'
)
BEGIN
    ALTER TABLE [Drivers] ADD [VehicleLicenseNumber] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507140035_AddDriverDocumentComplianceExpansion'
)
BEGIN
    CREATE TABLE [DriverDocumentReviews] (
        [Id] uniqueidentifier NOT NULL,
        [DriverId] uniqueidentifier NOT NULL,
        [Type] nvarchar(30) NOT NULL,
        [Decision] nvarchar(20) NOT NULL,
        [RejectionReason] nvarchar(1000) NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedByName] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DriverDocumentReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DriverDocumentReviews_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507140035_AddDriverDocumentComplianceExpansion'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DriverDocumentReviews_DriverId_Type] ON [DriverDocumentReviews] ([DriverId], [Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507140035_AddDriverDocumentComplianceExpansion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260507140035_AddDriverDocumentComplianceExpansion', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508165331_AddVendorProfitBasedCommission'
)
BEGIN
    ALTER TABLE [VendorProductBulkOperationItem] ADD [CostPrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508165331_AddVendorProfitBasedCommission'
)
BEGIN
    ALTER TABLE [VendorProductBulkOperationItem] ADD [TradePrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508165331_AddVendorProfitBasedCommission'
)
BEGIN
    ALTER TABLE [VendorProduct] ADD [CostPrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508165331_AddVendorProfitBasedCommission'
)
BEGIN
    ALTER TABLE [VendorProduct] ADD [TradePrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508165331_AddVendorProfitBasedCommission'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [TradeUnitPrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508165331_AddVendorProfitBasedCommission'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [VendorProfitPerUnit] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508165331_AddVendorProfitBasedCommission'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508165331_AddVendorProfitBasedCommission', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509083125_AddBrandCoverImageUrl'
)
BEGIN
    ALTER TABLE [Brand] ADD [CoverImageUrl] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509083125_AddBrandCoverImageUrl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260509083125_AddBrandCoverImageUrl', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509120611_AddOrderItemStockTracking'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [StockDeductedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509120611_AddOrderItemStockTracking'
)
BEGIN
    ALTER TABLE [OrderItems] ADD [StockRestoredAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260509120611_AddOrderItemStockTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260509120611_AddOrderItemStockTracking', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    ALTER TABLE [Wallet] ADD [CodOwedBalance] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    ALTER TABLE [Wallet] ADD [CurrencyCode] nvarchar(3) NOT NULL DEFAULT N'EGP';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    ALTER TABLE [Wallet] ADD [LastJournalSequence] bigint NOT NULL DEFAULT CAST(0 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE TABLE [FinancialEvents] (
        [Id] uniqueidentifier NOT NULL,
        [EventType] nvarchar(60) NOT NULL,
        [CorrelationId] uniqueidentifier NOT NULL,
        [IdempotencyKey] nvarchar(160) NOT NULL,
        [OrderId] uniqueidentifier NULL,
        [SettlementId] uniqueidentifier NULL,
        [PayoutId] uniqueidentifier NULL,
        [RefundId] uniqueidentifier NULL,
        [CurrencyCode] nvarchar(3) NOT NULL DEFAULT N'EGP',
        [OccurredAtUtc] datetime2 NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_FinancialEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE TABLE [JournalEntries] (
        [Id] uniqueidentifier NOT NULL,
        [FinancialEventId] uniqueidentifier NOT NULL,
        [SequenceNumber] bigint NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL DEFAULT N'EGP',
        [PostedAtUtc] datetime2 NOT NULL,
        [Memo] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalEntries_FinancialEvents_FinancialEventId] FOREIGN KEY ([FinancialEventId]) REFERENCES [FinancialEvents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE TABLE [JournalLines] (
        [Id] uniqueidentifier NOT NULL,
        [JournalEntryId] uniqueidentifier NOT NULL,
        [AccountCode] nvarchar(60) NOT NULL,
        [OwnerType] nvarchar(30) NULL,
        [OwnerId] uniqueidentifier NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL DEFAULT N'EGP',
        [OrderId] uniqueidentifier NULL,
        [SettlementId] uniqueidentifier NULL,
        [PayoutId] uniqueidentifier NULL,
        [Memo] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_JournalLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalLines_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE INDEX [IX_FinancialEvents_CorrelationId] ON [FinancialEvents] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FinancialEvents_IdempotencyKey] ON [FinancialEvents] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE INDEX [IX_FinancialEvents_OrderId] ON [FinancialEvents] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JournalEntries_FinancialEventId] ON [JournalEntries] ([FinancialEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JournalEntries_SequenceNumber] ON [JournalEntries] ([SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE INDEX [IX_JournalLines_AccountOwner] ON [JournalLines] ([AccountCode], [OwnerType], [OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE INDEX [IX_JournalLines_JournalEntryId] ON [JournalLines] ([JournalEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    CREATE INDEX [IX_JournalLines_OrderId] ON [JournalLines] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510071348_AddLedgerFirstFinanceSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260510071348_AddLedgerFirstFinanceSchema', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    DROP INDEX [IX_SettlementItems_SettlementId] ON [SettlementItems];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Settlements] ADD [AdjustmentAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Settlements] ADD [OwnerId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Settlements] ADD [OwnerType] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Settlements] ADD [PeriodFrom] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Settlements] ADD [PeriodTo] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Settlements] ADD [RecoveryAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Settlements] ADD [RefundAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Settlements] ADD [ResolutionType] nvarchar(40) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    DECLARE @var24 sysname;
    SELECT @var24 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SettlementItems]') AND [c].[name] = N'OrderId');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [SettlementItems] DROP CONSTRAINT [' + @var24 + '];');
    ALTER TABLE [SettlementItems] ALTER COLUMN [OrderId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [SettlementItems] ADD [Adjustment] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [SettlementItems] ADD [Amount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [SettlementItems] ADD [Commission] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [SettlementItems] ADD [LineType] nvarchar(40) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [SettlementItems] ADD [NetAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [SettlementItems] ADD [Recovery] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [SettlementItems] ADD [Refund] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [SettlementItems] ADD [SourceId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Payouts] ADD [CompletedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Payouts] ADD [DestinationSnapshot] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Payouts] ADD [DestinationType] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Payouts] ADD [FailureReason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Payouts] ADD [ProcessedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Payouts] ADD [ProviderName] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Payouts] ADD [ProviderTransferId] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    ALTER TABLE [Payouts] ADD [TriggeredAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    CREATE TABLE [PayoutAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [PayoutId] uniqueidentifier NOT NULL,
        [AttemptType] nvarchar(50) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [ProviderName] nvarchar(50) NOT NULL,
        [ProviderTransferId] nvarchar(200) NULL,
        [TransferReference] nvarchar(200) NULL,
        [FailureReason] nvarchar(1000) NULL,
        [RawPayload] nvarchar(4000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PayoutAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayoutAttempts_Payouts_PayoutId] FOREIGN KEY ([PayoutId]) REFERENCES [Payouts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    CREATE INDEX [IX_Settlements_Owner_Period] ON [Settlements] ([OwnerType], [OwnerId], [PeriodFrom], [PeriodTo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SettlementItems_Settlement_Source] ON [SettlementItems] ([SettlementId], [LineType], [SourceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payouts_ProviderTransferId] ON [Payouts] ([ProviderTransferId]) WHERE [ProviderTransferId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    CREATE INDEX [IX_PayoutAttempts_Payout_Attempt_ProviderTransfer] ON [PayoutAttempts] ([PayoutId], [AttemptType], [ProviderTransferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260510074114_CompleteLedgerFirstFinanceSystem'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260510074114_CompleteLedgerFirstFinanceSystem', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512120000_AddUserDirectoryProfile'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [Department] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512120000_AddUserDirectoryProfile'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [Team] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512120000_AddUserDirectoryProfile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512120000_AddUserDirectoryProfile', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512133000_HardenAdminAccessControl'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LastPasswordChangedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512133000_HardenAdminAccessControl'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512133000_HardenAdminAccessControl'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TemporaryPasswordIssuedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512133000_HardenAdminAccessControl'
)
BEGIN
    CREATE TABLE [AccessAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [TargetUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(100) NOT NULL,
        [Summary] nvarchar(500) NOT NULL,
        [BeforeJson] nvarchar(max) NULL,
        [AfterJson] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [IpAddress] nvarchar(100) NULL,
        [UserAgent] nvarchar(500) NULL,
        CONSTRAINT [PK_AccessAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccessAuditLogs_AspNetUsers_TargetUserId] FOREIGN KEY ([TargetUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512133000_HardenAdminAccessControl'
)
BEGIN
    CREATE INDEX [IX_AccessAuditLogs_ActorUserId] ON [AccessAuditLogs] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512133000_HardenAdminAccessControl'
)
BEGIN
    CREATE INDEX [IX_AccessAuditLogs_TargetUserId_CreatedAtUtc] ON [AccessAuditLogs] ([TargetUserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512133000_HardenAdminAccessControl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512133000_HardenAdminAccessControl', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512220500_SyncCatalogModelSnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512220500_SyncCatalogModelSnapshot', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512221000_AddBrandBulkCategoryIds'
)
BEGIN
    ALTER TABLE [AdminBrandBulkOperationItems] ADD [CategoryIdsJson] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512221000_AddBrandBulkCategoryIds'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512221000_AddBrandBulkCategoryIds', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513000100_EnsureBrandCategoriesTable'
)
BEGIN
    IF OBJECT_ID(N'[BrandCategories]', N'U') IS NULL
    BEGIN
        CREATE TABLE [BrandCategories] (
            [Id] uniqueidentifier NOT NULL,
            [BrandId] uniqueidentifier NOT NULL,
            [CategoryId] uniqueidentifier NOT NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_BrandCategories] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_BrandCategories_Brand_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brand] ([Id]) ON DELETE CASCADE,
            CONSTRAINT [FK_BrandCategories_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION
        );
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513000100_EnsureBrandCategoriesTable'
)
BEGIN
    IF OBJECT_ID(N'[BrandCategories]', N'U') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM sys.indexes
           WHERE name = N'IX_BrandCategories_BrandId_CategoryId'
           AND object_id = OBJECT_ID(N'[BrandCategories]')
       )
    BEGIN
        CREATE UNIQUE INDEX [IX_BrandCategories_BrandId_CategoryId] ON [BrandCategories] ([BrandId], [CategoryId]);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513000100_EnsureBrandCategoriesTable'
)
BEGIN
    IF OBJECT_ID(N'[BrandCategories]', N'U') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1 FROM sys.indexes
           WHERE name = N'IX_BrandCategories_CategoryId'
           AND object_id = OBJECT_ID(N'[BrandCategories]')
       )
    BEGIN
        CREATE INDEX [IX_BrandCategories_CategoryId] ON [BrandCategories] ([CategoryId]);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513000100_EnsureBrandCategoriesTable'
)
BEGIN
    IF OBJECT_ID(N'[BrandCategories]', N'U') IS NOT NULL
    BEGIN
        INSERT INTO [BrandCategories] ([Id], [BrandId], [CategoryId], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [brand].[Id], [brand].[CategoryId], SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [Brand] AS [brand]
        WHERE [brand].[CategoryId] IS NOT NULL
          AND NOT EXISTS (
              SELECT 1
              FROM [BrandCategories] AS [link]
              WHERE [link].[BrandId] = [brand].[Id]
                AND [link].[CategoryId] = [brand].[CategoryId]
          );
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513000100_EnsureBrandCategoriesTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513000100_EnsureBrandCategoriesTable', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AdminCatalogPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AdminDisputesPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AdminDriversPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AdminRefundsPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AdminSettlementsPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AdminSupportPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AdminSystemPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [AdminVendorsPushEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    CREATE TABLE [AdminAlertEvents] (
        [Id] uniqueidentifier NOT NULL,
        [Type] nvarchar(100) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [Priority] nvarchar(20) NOT NULL,
        [TitleAr] nvarchar(200) NOT NULL,
        [TitleEn] nvarchar(200) NOT NULL,
        [BodyAr] nvarchar(1000) NOT NULL,
        [BodyEn] nvarchar(1000) NOT NULL,
        [ReferenceId] uniqueidentifier NULL,
        [TargetUrl] nvarchar(500) NOT NULL,
        [DataJson] nvarchar(4000) NOT NULL,
        [DedupeKey] nvarchar(300) NOT NULL,
        [SuppressPush] bit NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [Attempts] int NOT NULL,
        [NextAttemptAtUtc] datetime2 NULL,
        [LastAttemptAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [LastError] nvarchar(2000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminAlertEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    CREATE TABLE [AdminAlertDispatches] (
        [Id] uniqueidentifier NOT NULL,
        [AdminAlertEventId] uniqueidentifier NOT NULL,
        [AdminUserId] uniqueidentifier NOT NULL,
        [NotificationId] uniqueidentifier NULL,
        [Status] nvarchar(30) NOT NULL,
        [SignalRSent] bit NOT NULL,
        [PushAttempted] bit NOT NULL,
        [PushSent] bit NOT NULL,
        [PushSkipped] bit NOT NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(1000) NULL,
        [LastAttemptAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminAlertDispatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AdminAlertDispatches_AdminAlertEvents_AdminAlertEventId] FOREIGN KEY ([AdminAlertEventId]) REFERENCES [AdminAlertEvents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AdminAlertDispatches_AdminAlertEventId_AdminUserId] ON [AdminAlertDispatches] ([AdminAlertEventId], [AdminUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    CREATE INDEX [IX_AdminAlertDispatches_AdminUserId_CreatedAtUtc] ON [AdminAlertDispatches] ([AdminUserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    CREATE INDEX [IX_AdminAlertDispatches_NotificationId] ON [AdminAlertDispatches] ([NotificationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    CREATE INDEX [IX_AdminAlertEvents_DedupeKey_CreatedAtUtc] ON [AdminAlertEvents] ([DedupeKey], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    CREATE INDEX [IX_AdminAlertEvents_Status_NextAttemptAtUtc_CreatedAtUtc] ON [AdminAlertEvents] ([Status], [NextAttemptAtUtc], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    CREATE INDEX [IX_AdminAlertEvents_Type_ReferenceId_CreatedAtUtc] ON [AdminAlertEvents] ([Type], [ReferenceId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513092824_AddAdminAlertOutbox'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513092824_AddAdminAlertOutbox', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513202510_AddFeaturedProductSelectionSettings'
)
BEGIN
    CREATE TABLE [FeaturedProductSelectionSettings] (
        [Id] uniqueidentifier NOT NULL,
        [SelectionMode] nvarchar(50) NOT NULL,
        [TargetCount] int NOT NULL DEFAULT 10,
        [MinSalesCount] int NOT NULL DEFAULT 1,
        [MinStoreCount] int NOT NULL DEFAULT 2,
        [RequireDiscount] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ExcludeProductsAlreadyInSpecialOffers] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_FeaturedProductSelectionSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513202510_AddFeaturedProductSelectionSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513202510_AddFeaturedProductSelectionSettings', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514084213_SyncPendingModelChanges'
)
BEGIN
    ALTER TABLE [Vendor] ADD [NotificationSound] nvarchar(32) NOT NULL DEFAULT N'classic';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514084213_SyncPendingModelChanges'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [NotificationSound] nvarchar(32) NOT NULL DEFAULT N'classic';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514084213_SyncPendingModelChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514084213_SyncPendingModelChanges', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514143000_AddVendorProfileReviewItems'
)
BEGIN
    CREATE TABLE [VendorProfileReviewItems] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [Code] nvarchar(120) NOT NULL,
        [TargetType] nvarchar(20) NOT NULL,
        [Step] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [DecisionNote] nvarchar(2000) NULL,
        [LastSubmittedAtUtc] datetime2 NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [ReviewedByUserId] uniqueidentifier NULL,
        [ReviewedByName] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorProfileReviewItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorProfileReviewItems_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514143000_AddVendorProfileReviewItems'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_VendorProfileReviewItems_VendorId_Code] ON [VendorProfileReviewItems] ([VendorId], [Code]) WHERE [VendorId] IS NOT NULL AND [Code] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514143000_AddVendorProfileReviewItems'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514143000_AddVendorProfileReviewItems', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [ActualAssignedDriverPickupDistanceKm] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [ActualDispatchDeviationPercent] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [DeliveryQuoteLockedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [DeliveryQuoteStatus] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [DeliveryQuoteVersion] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [DriverToVendorDistanceKm] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [DriverToVendorFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [DriverToVendorPricingSource] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [HasDeliveryAnomalyWarning] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [PricingOriginDriverId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [PricingOriginType] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [UsedEstimatedDriverPricing] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [VendorToCustomerDistanceKm] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [VendorToCustomerFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Orders] ADD [VendorToCustomerPricingSource] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [DeliveryQuoteLockedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [DeliveryQuoteStatus] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [DeliveryQuoteVersion] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [DriverToVendorDistanceKm] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [DriverToVendorFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [DriverToVendorPricingSource] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [HasDeliveryAnomalyWarning] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [PricingOriginDriverId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [PricingOriginType] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [UsedEstimatedDriverPricing] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [VendorToCustomerDistanceKm] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [VendorToCustomerFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    ALTER TABLE [Carts] ADD [VendorToCustomerPricingSource] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    CREATE TABLE [CityDeliveryPricingSettings] (
        [Id] uniqueidentifier NOT NULL,
        [SaudiCityId] uniqueidentifier NOT NULL,
        [BaseDeliveryFee] decimal(18,2) NOT NULL,
        [IncludedKm] decimal(18,2) NOT NULL,
        [ExtraKmFee] decimal(18,2) NOT NULL,
        [MinDeliveryFee] decimal(18,2) NOT NULL,
        [MaxDeliveryFee] decimal(18,2) NOT NULL,
        [IsPricingActive] bit NOT NULL,
        [VatPercent] decimal(18,2) NOT NULL,
        [CodFeeType] nvarchar(20) NOT NULL,
        [CodFlatFee] decimal(18,2) NOT NULL,
        [CodPercent] decimal(18,2) NOT NULL,
        [IsVatActive] bit NOT NULL,
        [IsCodFeeActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CityDeliveryPricingSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    CREATE TABLE [DeliveryPricingDefaults] (
        [Id] uniqueidentifier NOT NULL,
        [BaseDeliveryFee] decimal(18,2) NOT NULL,
        [IncludedKm] decimal(18,2) NOT NULL,
        [ExtraKmFee] decimal(18,2) NOT NULL,
        [MinDeliveryFee] decimal(18,2) NOT NULL,
        [MaxDeliveryFee] decimal(18,2) NOT NULL,
        [IsPricingActive] bit NOT NULL,
        [VatPercent] decimal(18,2) NOT NULL,
        [CodFeeType] nvarchar(20) NOT NULL,
        [CodFlatFee] decimal(18,2) NOT NULL,
        [CodPercent] decimal(18,2) NOT NULL,
        [IsVatActive] bit NOT NULL,
        [IsCodFeeActive] bit NOT NULL,
        [MinTotalDeliveryFee] decimal(18,2) NOT NULL,
        [MaxTotalDeliveryFee] decimal(18,2) NOT NULL,
        [MaxQuotedDistanceKm] decimal(18,2) NOT NULL,
        [WarningSubtotalRatioThreshold] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DeliveryPricingDefaults] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    CREATE TABLE [RegionDeliveryPricingSettings] (
        [Id] uniqueidentifier NOT NULL,
        [SaudiRegionId] uniqueidentifier NOT NULL,
        [BaseDeliveryFee] decimal(18,2) NOT NULL,
        [IncludedKm] decimal(18,2) NOT NULL,
        [ExtraKmFee] decimal(18,2) NOT NULL,
        [MinDeliveryFee] decimal(18,2) NOT NULL,
        [MaxDeliveryFee] decimal(18,2) NOT NULL,
        [IsPricingActive] bit NOT NULL,
        [VatPercent] decimal(18,2) NOT NULL,
        [CodFeeType] nvarchar(20) NOT NULL,
        [CodFlatFee] decimal(18,2) NOT NULL,
        [CodPercent] decimal(18,2) NOT NULL,
        [IsVatActive] bit NOT NULL,
        [IsCodFeeActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RegionDeliveryPricingSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CityDeliveryPricingSettings_SaudiCityId] ON [CityDeliveryPricingSettings] ([SaudiCityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RegionDeliveryPricingSettings_SaudiRegionId] ON [RegionDeliveryPricingSettings] ([SaudiRegionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260514150110_AddDeliveryPricingPolicyDefaults'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260514150110_AddDeliveryPricingPolicyDefaults', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    ALTER TABLE [UnitOfMeasure] ADD [Kind] nvarchar(30) NOT NULL DEFAULT N'Measurement';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD [VariantGroupId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD [PackageTypeId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD [MeasurementValue] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD [MeasurementUnitId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    UPDATE UnitOfMeasure
    SET Kind = CASE
        WHEN NameEn IN ('Piece','Pack','Box','Carton','Case','Bottle','Jar','Can','Pouch','Sachet','Bag','Roll','Sheet','Pair','Set','Bundle','Dozen','Tray','Crate','Pallet','Strip','Blister','Tube','Bar','Loaf','Slice','Capsule','Tablet','Vial','Ampoule')
            THEN 'Packaging'
        ELSE 'Measurement'
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    UPDATE MasterProduct
    SET VariantGroupId = Id
    WHERE VariantGroupId = '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    UPDATE mp
    SET MeasurementUnitId = mp.UnitOfMeasureId
    FROM MasterProduct mp
    INNER JOIN UnitOfMeasure u ON u.Id = mp.UnitOfMeasureId
    WHERE u.Kind = 'Measurement' AND mp.UnitOfMeasureId IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    UPDATE mp
    SET PackageTypeId = mp.UnitOfMeasureId
    FROM MasterProduct mp
    INNER JOIN UnitOfMeasure u ON u.Id = mp.UnitOfMeasureId
    WHERE u.Kind = 'Packaging' AND mp.UnitOfMeasureId IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    CREATE INDEX [IX_MasterProduct_MeasurementUnitId] ON [MasterProduct] ([MeasurementUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    CREATE INDEX [IX_MasterProduct_PackageTypeId] ON [MasterProduct] ([PackageTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    CREATE INDEX [IX_MasterProduct_VariantGroupId] ON [MasterProduct] ([VariantGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD CONSTRAINT [FK_MasterProduct_UnitOfMeasure_MeasurementUnitId] FOREIGN KEY ([MeasurementUnitId]) REFERENCES [UnitOfMeasure] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD CONSTRAINT [FK_MasterProduct_UnitOfMeasure_PackageTypeId] FOREIGN KEY ([PackageTypeId]) REFERENCES [UnitOfMeasure] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260515000100_AddProductPackagingAndMeasurementVariants'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260515000100_AddProductPackagingAndMeasurementVariants', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516203430_AddSystemLogEntries'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516203430_AddSystemLogEntries', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516222743_SyncSystemLogsModel'
)
BEGIN
    CREATE TABLE [SystemLogEntries] (
        [Id] uniqueidentifier NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [SourceApp] nvarchar(50) NOT NULL,
        [Module] nvarchar(50) NOT NULL,
        [Action] nvarchar(150) NOT NULL,
        [Summary] nvarchar(500) NOT NULL,
        [RequestPath] nvarchar(300) NOT NULL,
        [HttpMethod] nvarchar(16) NOT NULL,
        [StatusCode] int NOT NULL,
        [IsSuccess] bit NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [ActorFullName] nvarchar(200) NULL,
        [ActorEmail] nvarchar(256) NULL,
        [ActorRole] nvarchar(100) NULL,
        [TargetEntityType] nvarchar(100) NULL,
        [TargetEntityId] nvarchar(100) NULL,
        [CorrelationId] nvarchar(100) NULL,
        [IpAddress] nvarchar(100) NULL,
        [UserAgent] nvarchar(500) NULL,
        [QueryString] nvarchar(1000) NULL,
        [RequestPayloadJson] nvarchar(max) NULL,
        [MetadataJson] nvarchar(max) NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        CONSTRAINT [PK_SystemLogEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516222743_SyncSystemLogsModel'
)
BEGIN
    CREATE INDEX [IX_SystemLogEntries_ActorUserId] ON [SystemLogEntries] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516222743_SyncSystemLogsModel'
)
BEGIN
    CREATE INDEX [IX_SystemLogEntries_IsSuccess] ON [SystemLogEntries] ([IsSuccess]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516222743_SyncSystemLogsModel'
)
BEGIN
    CREATE INDEX [IX_SystemLogEntries_OccurredAtUtc] ON [SystemLogEntries] ([OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516222743_SyncSystemLogsModel'
)
BEGIN
    CREATE INDEX [IX_SystemLogEntries_SourceApp_Module_OccurredAtUtc] ON [SystemLogEntries] ([SourceApp], [Module], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516222743_SyncSystemLogsModel'
)
BEGIN
    CREATE INDEX [IX_SystemLogEntries_TargetEntityType_TargetEntityId] ON [SystemLogEntries] ([TargetEntityType], [TargetEntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516222743_SyncSystemLogsModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516222743_SyncSystemLogsModel', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517095829_AddOrderEtaSnapshotFields'
)
BEGIN
    ALTER TABLE [Orders] ADD [EtaCalculatedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517095829_AddOrderEtaSnapshotFields'
)
BEGIN
    ALTER TABLE [Orders] ADD [EtaCalculationMode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517095829_AddOrderEtaSnapshotFields'
)
BEGIN
    ALTER TABLE [Orders] ADD [EtaConfidence] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517095829_AddOrderEtaSnapshotFields'
)
BEGIN
    ALTER TABLE [Orders] ADD [EtaExplanation] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517095829_AddOrderEtaSnapshotFields'
)
BEGIN
    ALTER TABLE [Orders] ADD [EtaIsApproximate] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517095829_AddOrderEtaSnapshotFields'
)
BEGIN
    ALTER TABLE [Orders] ADD [EtaMaxMinutes] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517095829_AddOrderEtaSnapshotFields'
)
BEGIN
    ALTER TABLE [Orders] ADD [EtaMinMinutes] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517095829_AddOrderEtaSnapshotFields'
)
BEGIN
    ALTER TABLE [Orders] ADD [EtaSource] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260517095829_AddOrderEtaSnapshotFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260517095829_AddOrderEtaSnapshotFields', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE TABLE [PaymentGatewaySettlements] (
        [Id] uniqueidentifier NOT NULL,
        [ProviderName] nvarchar(40) NOT NULL,
        [ProviderSettlementId] nvarchar(200) NOT NULL,
        [SettlementDate] datetime2 NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL DEFAULT N'SAR',
        [GrossAmount] decimal(18,2) NOT NULL,
        [FeeAmount] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [RawFileOrJson] nvarchar(max) NULL,
        [Notes] nvarchar(1000) NULL,
        [FinancialEventId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentGatewaySettlements] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE TABLE [PaymentProviderEventInbox] (
        [Id] uniqueidentifier NOT NULL,
        [ProviderName] nvarchar(40) NOT NULL,
        [ProviderEventId] nvarchar(200) NOT NULL,
        [EventType] nvarchar(120) NOT NULL,
        [ProviderPaymentId] nvarchar(200) NULL,
        [SecretValid] bit NOT NULL,
        [RawPayload] nvarchar(max) NOT NULL,
        [Headers] nvarchar(max) NULL,
        [Status] nvarchar(30) NOT NULL,
        [FailureReason] nvarchar(1000) NULL,
        [ReceivedAtUtc] datetime2 NOT NULL,
        [ProcessingStartedAtUtc] datetime2 NULL,
        [ProcessedAtUtc] datetime2 NULL,
        [ProcessingAttempts] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentProviderEventInbox] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE TABLE [RefundAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [RefundId] uniqueidentifier NOT NULL,
        [ProductAmount] decimal(18,2) NOT NULL,
        [DeliveryAmount] decimal(18,2) NOT NULL,
        [VatAmount] decimal(18,2) NOT NULL,
        [CodFeeAmount] decimal(18,2) NOT NULL,
        [PlatformAbsorbedAmount] decimal(18,2) NOT NULL,
        [VendorRecoveryAmount] decimal(18,2) NOT NULL,
        [DriverRecoveryAmount] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL DEFAULT N'SAR',
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RefundAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefundAllocations_Refunds_RefundId] FOREIGN KEY ([RefundId]) REFERENCES [Refunds] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE TABLE [WalletHolds] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerType] nvarchar(30) NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL DEFAULT N'SAR',
        [Reason] nvarchar(30) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [ReferenceType] nvarchar(80) NULL,
        [ReferenceId] uniqueidentifier NULL,
        [IdempotencyKey] nvarchar(160) NOT NULL,
        [CreatedAtUtcOnHold] datetime2 NOT NULL,
        [ReleasedAtUtc] datetime2 NULL,
        [ConsumedAtUtc] datetime2 NULL,
        [CancelledAtUtc] datetime2 NULL,
        [ExpiresAtUtc] datetime2 NULL,
        [FailureReason] nvarchar(500) NULL,
        [Memo] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_WalletHolds] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE TABLE [PaymentGatewaySettlementItems] (
        [Id] uniqueidentifier NOT NULL,
        [SettlementId] uniqueidentifier NOT NULL,
        [ProviderPaymentId] nvarchar(200) NOT NULL,
        [OrderId] uniqueidentifier NULL,
        [PaymentId] uniqueidentifier NULL,
        [GrossAmount] decimal(18,2) NOT NULL,
        [FeeAmount] decimal(18,2) NOT NULL,
        [NetAmount] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL DEFAULT N'SAR',
        [ProviderCreatedAtUtc] datetime2 NULL,
        [Metadata] nvarchar(max) NULL,
        [MatchStatus] nvarchar(30) NOT NULL,
        [MatchNote] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentGatewaySettlementItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentGatewaySettlementItems_PaymentGatewaySettlements_SettlementId] FOREIGN KEY ([SettlementId]) REFERENCES [PaymentGatewaySettlements] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE INDEX [IX_PaymentGatewaySettlementItems_OrderId] ON [PaymentGatewaySettlementItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE INDEX [IX_PaymentGatewaySettlementItems_Settlement_PaymentId] ON [PaymentGatewaySettlementItems] ([SettlementId], [ProviderPaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentGatewaySettlements_Provider_Id] ON [PaymentGatewaySettlements] ([ProviderName], [ProviderSettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentProviderEventInbox_Provider_EventId] ON [PaymentProviderEventInbox] ([ProviderName], [ProviderEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE INDEX [IX_PaymentProviderEventInbox_Provider_PaymentId] ON [PaymentProviderEventInbox] ([ProviderName], [ProviderPaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE INDEX [IX_PaymentProviderEventInbox_Status] ON [PaymentProviderEventInbox] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefundAllocations_RefundId] ON [RefundAllocations] ([RefundId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WalletHolds_IdempotencyKey] ON [WalletHolds] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE INDEX [IX_WalletHolds_Owner_Status] ON [WalletHolds] ([OwnerType], [OwnerId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    CREATE INDEX [IX_WalletHolds_Reference] ON [WalletHolds] ([ReferenceType], [ReferenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518143923_AddFinancialFoundationTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518143923_AddFinancialFoundationTables', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [ApprovedAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [CompensationMethod] nvarchar(30) NOT NULL DEFAULT N'SameMethod';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [Currency] nvarchar(3) NOT NULL DEFAULT N'SAR';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [FailedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [LifecycleStatus] nvarchar(30) NOT NULL DEFAULT N'Requested';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [ProviderName] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [ProviderRefundId] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [RawProviderResponse] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [RequestedAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Refunds] ADD [SucceededAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Payments] ADD [Currency] nvarchar(3) NOT NULL DEFAULT N'SAR';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Payments] ADD [IdempotencyKey] nvarchar(160) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Payments] ADD [ProviderInvoiceId] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Payments] ADD [ProviderMethod] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Payments] ADD [ProviderReferenceNumber] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Payments] ADD [ProviderStatus] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Payments] ADD [RawCreateResponse] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Payments] ADD [RawFetchResponse] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Orders] ADD [CommissionPolicySnapshot] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Orders] ADD [Currency] nvarchar(3) NOT NULL DEFAULT N'SAR';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Orders] ADD [DriverCommissionAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Orders] ADD [PricingMode] nvarchar(16) NOT NULL DEFAULT N'live';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Orders] ADD [ProductGross] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Orders] ADD [ProductNet] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Orders] ADD [TaxPolicySnapshot] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    ALTER TABLE [Orders] ADD [VendorCommissionAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN

                    UPDATE [Orders] SET
                        [ProductGross]            = [Subtotal],
                        [ProductNet]              = CASE WHEN ([Subtotal] - [DiscountTotal]) < 0 THEN 0 ELSE ([Subtotal] - [DiscountTotal]) END,
                        [VendorCommissionAmount]  = [CommissionAmount]
                    WHERE [ProductGross] = 0 AND [ProductNet] = 0 AND [VendorCommissionAmount] = 0;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Refunds_Provider_RefundId] ON [Refunds] ([ProviderName], [ProviderRefundId]) WHERE [ProviderRefundId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_IdempotencyKey] ON [Payments] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Payments_Provider_Transaction] ON [Payments] ([ProviderName], [ProviderTransactionId]) WHERE [ProviderTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518145748_ExtendOrderPaymentRefundForSarWorkflow', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518153755_SwitchCurrencyDefaultsToSar'
)
BEGIN
    DECLARE @var25 sysname;
    SELECT @var25 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Wallet]') AND [c].[name] = N'CurrencyCode');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [Wallet] DROP CONSTRAINT [' + @var25 + '];');
    ALTER TABLE [Wallet] ADD DEFAULT N'SAR' FOR [CurrencyCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518153755_SwitchCurrencyDefaultsToSar'
)
BEGIN
    DECLARE @var26 sysname;
    SELECT @var26 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JournalLines]') AND [c].[name] = N'CurrencyCode');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [JournalLines] DROP CONSTRAINT [' + @var26 + '];');
    ALTER TABLE [JournalLines] ADD DEFAULT N'SAR' FOR [CurrencyCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518153755_SwitchCurrencyDefaultsToSar'
)
BEGIN
    DECLARE @var27 sysname;
    SELECT @var27 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JournalEntries]') AND [c].[name] = N'CurrencyCode');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [JournalEntries] DROP CONSTRAINT [' + @var27 + '];');
    ALTER TABLE [JournalEntries] ADD DEFAULT N'SAR' FOR [CurrencyCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518153755_SwitchCurrencyDefaultsToSar'
)
BEGIN
    DECLARE @var28 sysname;
    SELECT @var28 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FinancialEvents]') AND [c].[name] = N'CurrencyCode');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [FinancialEvents] DROP CONSTRAINT [' + @var28 + '];');
    ALTER TABLE [FinancialEvents] ADD DEFAULT N'SAR' FOR [CurrencyCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518153755_SwitchCurrencyDefaultsToSar'
)
BEGIN
    UPDATE [Wallet] SET [CurrencyCode] = 'SAR' WHERE [CurrencyCode] = 'EGP';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518153755_SwitchCurrencyDefaultsToSar'
)
BEGIN
    UPDATE [JournalLines] SET [CurrencyCode] = 'SAR' WHERE [CurrencyCode] = 'EGP';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518153755_SwitchCurrencyDefaultsToSar'
)
BEGIN
    UPDATE [JournalEntries] SET [CurrencyCode] = 'SAR' WHERE [CurrencyCode] = 'EGP';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518153755_SwitchCurrencyDefaultsToSar'
)
BEGIN
    UPDATE [FinancialEvents] SET [CurrencyCode] = 'SAR' WHERE [CurrencyCode] = 'EGP';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518153755_SwitchCurrencyDefaultsToSar'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518153755_SwitchCurrencyDefaultsToSar', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519083238_AddMoyasarPayoutsAndDriverWithdrawalLink'
)
BEGIN
    ALTER TABLE [Payouts] ADD [ProviderSequenceNumber] nvarchar(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519083238_AddMoyasarPayoutsAndDriverWithdrawalLink'
)
BEGIN
    ALTER TABLE [DriverWithdrawalRequests] ADD [PayoutId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519083238_AddMoyasarPayoutsAndDriverWithdrawalLink'
)
BEGIN
    CREATE INDEX [IX_DriverWithdrawalRequests_PayoutId] ON [DriverWithdrawalRequests] ([PayoutId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519083238_AddMoyasarPayoutsAndDriverWithdrawalLink'
)
BEGIN
    ALTER TABLE [DriverWithdrawalRequests] ADD CONSTRAINT [FK_DriverWithdrawalRequests_Payouts_PayoutId] FOREIGN KEY ([PayoutId]) REFERENCES [Payouts] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519083238_AddMoyasarPayoutsAndDriverWithdrawalLink'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519083238_AddMoyasarPayoutsAndDriverWithdrawalLink', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519120854_AddPlatformBankAccount'
)
BEGIN
    CREATE TABLE [PlatformBankAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [BankName] nvarchar(200) NOT NULL,
        [AccountHolderName] nvarchar(200) NOT NULL,
        [IBAN] nvarchar(34) NOT NULL,
        [AccountNumber] nvarchar(64) NULL,
        [CountryCode] nvarchar(2) NOT NULL DEFAULT N'SA',
        [City] nvarchar(100) NOT NULL DEFAULT N'Riyadh',
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsBankTransferEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsMoyasarPayoutsEnabled] bit NOT NULL DEFAULT CAST(0 AS bit),
        [MoyasarPayoutSourceId] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PlatformBankAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519120854_AddPlatformBankAccount'
)
BEGIN
    CREATE INDEX [IX_PlatformBankAccounts_IsActive] ON [PlatformBankAccounts] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519120854_AddPlatformBankAccount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519120854_AddPlatformBankAccount', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520000100_AddMasterProductCardPriceVisibility'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD [ShowPriceOnCard] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520000100_AddMasterProductCardPriceVisibility'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260520000100_AddMasterProductCardPriceVisibility', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520093000_AddDriverAccountSupportCases'
)
BEGIN
    ALTER TABLE [OrderSupportCases] DROP CONSTRAINT [FK_OrderSupportCases_Orders_OrderId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520093000_AddDriverAccountSupportCases'
)
BEGIN
    DROP INDEX [IX_OrderSupportCases_OrderId_Status] ON [OrderSupportCases];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520093000_AddDriverAccountSupportCases'
)
BEGIN
    DECLARE @var29 sysname;
    SELECT @var29 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderSupportCases]') AND [c].[name] = N'OrderId');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [OrderSupportCases] DROP CONSTRAINT [' + @var29 + '];');
    ALTER TABLE [OrderSupportCases] ALTER COLUMN [OrderId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520093000_AddDriverAccountSupportCases'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD [DriverId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520093000_AddDriverAccountSupportCases'
)
BEGIN
    CREATE INDEX [IX_OrderSupportCases_DriverId_Type_Status] ON [OrderSupportCases] ([DriverId], [Type], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520093000_AddDriverAccountSupportCases'
)
BEGIN
    CREATE INDEX [IX_OrderSupportCases_OrderId_Status] ON [OrderSupportCases] ([OrderId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520093000_AddDriverAccountSupportCases'
)
BEGIN
    ALTER TABLE [OrderSupportCases] ADD CONSTRAINT [FK_OrderSupportCases_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520093000_AddDriverAccountSupportCases'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260520093000_AddDriverAccountSupportCases', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523092532_AddVendorSupportTickets'
)
BEGIN
    CREATE TABLE [VendorSupportTickets] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [AssignedAdminId] uniqueidentifier NULL,
        [AssignedAtUtc] datetime2 NULL,
        [Reference] nvarchar(40) NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [Priority] nvarchar(30) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [LastMessagePreview] nvarchar(200) NOT NULL,
        [FirstResponseAtUtc] datetime2 NULL,
        [ClosedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorSupportTickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorSupportTickets_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_VendorSupportTickets_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523092532_AddVendorSupportTickets'
)
BEGIN
    CREATE TABLE [VendorSupportTicketMessages] (
        [Id] uniqueidentifier NOT NULL,
        [VendorSupportTicketId] uniqueidentifier NOT NULL,
        [AuthorUserId] uniqueidentifier NULL,
        [AuthorRole] nvarchar(20) NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorSupportTicketMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorSupportTicketMessages_VendorSupportTickets_VendorSupportTicketId] FOREIGN KEY ([VendorSupportTicketId]) REFERENCES [VendorSupportTickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523092532_AddVendorSupportTickets'
)
BEGIN
    CREATE INDEX [IX_VendorSupportTicketMessages_AuthorUserId] ON [VendorSupportTicketMessages] ([AuthorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523092532_AddVendorSupportTickets'
)
BEGIN
    CREATE INDEX [IX_VendorSupportTicketMessages_VendorSupportTicketId_CreatedAtUtc] ON [VendorSupportTicketMessages] ([VendorSupportTicketId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523092532_AddVendorSupportTickets'
)
BEGIN
    CREATE INDEX [IX_VendorSupportTickets_OrderId] ON [VendorSupportTickets] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523092532_AddVendorSupportTickets'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorSupportTickets_Reference] ON [VendorSupportTickets] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523092532_AddVendorSupportTickets'
)
BEGIN
    CREATE INDEX [IX_VendorSupportTickets_VendorId_Status_UpdatedAtUtc] ON [VendorSupportTickets] ([VendorId], [Status], [UpdatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523092532_AddVendorSupportTickets'
)
BEGIN
    CREATE INDEX [IX_VendorSupportTickets_VendorId_UpdatedAtUtc] ON [VendorSupportTickets] ([VendorId], [UpdatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523092532_AddVendorSupportTickets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523092532_AddVendorSupportTickets', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523202149_AddDriverLatestLocationAndIndexes'
)
BEGIN
    DROP INDEX [IX_DriverLocations_DriverId] ON [DriverLocations];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523202149_AddDriverLatestLocationAndIndexes'
)
BEGIN
    CREATE TABLE [DriverLatestLocations] (
        [DriverId] uniqueidentifier NOT NULL,
        [Latitude] decimal(10,7) NOT NULL,
        [Longitude] decimal(10,7) NOT NULL,
        [AccuracyMeters] decimal(8,2) NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DriverLatestLocations] PRIMARY KEY ([DriverId]),
        CONSTRAINT [FK_DriverLatestLocations_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523202149_AddDriverLatestLocationAndIndexes'
)
BEGIN
    CREATE INDEX [IX_DriverLocations_DriverId_RecordedAt_Desc] ON [DriverLocations] ([DriverId], [RecordedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523202149_AddDriverLatestLocationAndIndexes'
)
BEGIN

                    ;WITH Latest AS (
                        SELECT
                            DriverId,
                            Latitude,
                            Longitude,
                            AccuracyMeters,
                            RecordedAtUtc,
                            ROW_NUMBER() OVER (PARTITION BY DriverId ORDER BY RecordedAtUtc DESC) AS rn
                        FROM dbo.DriverLocations
                    )
                    INSERT INTO dbo.DriverLatestLocations
                        (DriverId, Latitude, Longitude, AccuracyMeters, RecordedAtUtc, UpdatedAtUtc)
                    SELECT
                        DriverId, Latitude, Longitude, AccuracyMeters, RecordedAtUtc, SYSUTCDATETIME()
                    FROM Latest
                    WHERE rn = 1
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.DriverLatestLocations l WHERE l.DriverId = Latest.DriverId
                      );
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523202149_AddDriverLatestLocationAndIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523202149_AddDriverLatestLocationAndIndexes', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523203005_AddDriverNationalIdHash'
)
BEGIN
    ALTER TABLE [Drivers] ADD [NationalIdHash] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523203005_AddDriverNationalIdHash'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Drivers_NationalIdHash] ON [Drivers] ([NationalIdHash]) WHERE [NationalIdHash] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523203005_AddDriverNationalIdHash'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523203005_AddDriverNationalIdHash', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112145_AddMissingOtpAttemptColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524112145_AddMissingOtpAttemptColumns', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524120000_AddWalletTransactionJournalLineReferenceIndex'
)
BEGIN
    IF OBJECT_ID('tempdb..#DuplicateJournalLineTransactions') IS NOT NULL
        DROP TABLE #DuplicateJournalLineTransactions;

    SELECT
        Id,
        WalletId,
        Amount,
        Direction,
        TxnType
    INTO #DuplicateJournalLineTransactions
    FROM
    (
        SELECT
            Id,
            WalletId,
            Amount,
            Direction,
            TxnType,
            ROW_NUMBER() OVER (
                PARTITION BY ReferenceType, ReferenceId
                ORDER BY CreatedAtUtc, Id
            ) AS RowNumber
        FROM [WalletTransactions]
        WHERE ReferenceType = 'JournalLine'
            AND ReferenceId IS NOT NULL
    ) duplicate
    WHERE duplicate.RowNumber > 1;

    UPDATE wallet
    SET
        CurrentBalance = wallet.CurrentBalance + adjustments.CurrentBalanceAdjustment,
        CodOwedBalance = wallet.CodOwedBalance + adjustments.CodOwedBalanceAdjustment
    FROM [Wallet] wallet
    INNER JOIN
    (
        SELECT
            WalletId,
            SUM(CASE
                WHEN TxnType <> 'CashCollected' AND Direction = 'IN' THEN -Amount
                WHEN TxnType <> 'CashCollected' AND Direction = 'OUT' THEN Amount
                ELSE 0
            END) AS CurrentBalanceAdjustment,
            SUM(CASE
                WHEN TxnType = 'CashCollected' AND Direction = 'OUT' THEN -Amount
                WHEN TxnType = 'CashCollected' AND Direction = 'IN' THEN Amount
                ELSE 0
            END) AS CodOwedBalanceAdjustment
        FROM #DuplicateJournalLineTransactions
        GROUP BY WalletId
    ) adjustments ON adjustments.WalletId = wallet.Id;

    DELETE txn
    FROM [WalletTransactions] txn
    INNER JOIN #DuplicateJournalLineTransactions duplicate
        ON duplicate.Id = txn.Id;

    DROP TABLE #DuplicateJournalLineTransactions;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524120000_AddWalletTransactionJournalLineReferenceIndex'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_WalletTransactions_JournalLineReference] ON [WalletTransactions] ([ReferenceType], [ReferenceId]) WHERE [ReferenceType] = ''JournalLine'' AND [ReferenceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524120000_AddWalletTransactionJournalLineReferenceIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524120000_AddWalletTransactionJournalLineReferenceIndex', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524133527_AddSoftDeleteToCatalog'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD [DeletedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524133527_AddSoftDeleteToCatalog'
)
BEGIN
    ALTER TABLE [MasterProduct] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524133527_AddSoftDeleteToCatalog'
)
BEGIN
    ALTER TABLE [Category] ADD [DeletedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524133527_AddSoftDeleteToCatalog'
)
BEGIN
    ALTER TABLE [Category] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524133527_AddSoftDeleteToCatalog'
)
BEGIN
    ALTER TABLE [Brand] ADD [DeletedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524133527_AddSoftDeleteToCatalog'
)
BEGIN
    ALTER TABLE [Brand] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524133527_AddSoftDeleteToCatalog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524133527_AddSoftDeleteToCatalog', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525075829_SyncPendingModelChanges_May25'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshToken_Token' AND object_id = OBJECT_ID(N'[dbo].[RefreshToken]'))
        DROP INDEX [IX_RefreshToken_Token] ON [dbo].[RefreshToken];

    IF OBJECT_ID(N'[dbo].[RefreshToken]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[RefreshTokens]', N'U') IS NULL
    BEGIN
        IF OBJECT_ID(N'[dbo].[FK_RefreshToken_AspNetUsers_UserId]', N'F') IS NOT NULL
            ALTER TABLE [dbo].[RefreshToken] DROP CONSTRAINT [FK_RefreshToken_AspNetUsers_UserId];

        IF OBJECT_ID(N'[dbo].[PK_RefreshToken]', N'PK') IS NOT NULL
            ALTER TABLE [dbo].[RefreshToken] DROP CONSTRAINT [PK_RefreshToken];

        EXEC sp_rename N'[dbo].[RefreshToken]', N'RefreshTokens';

        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshToken_UserId' AND object_id = OBJECT_ID(N'[dbo].[RefreshTokens]'))
            EXEC sp_rename N'[dbo].[RefreshTokens].[IX_RefreshToken_UserId]', N'IX_RefreshTokens_UserId', N'INDEX';
    END

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshToken_Token' AND object_id = OBJECT_ID(N'[dbo].[RefreshTokens]'))
        DROP INDEX [IX_RefreshToken_Token] ON [dbo].[RefreshTokens];

    IF OBJECT_ID(N'[dbo].[RefreshTokens]', N'U') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'[dbo].[RefreshTokens]', N'Token') IS NOT NULL
            ALTER TABLE [dbo].[RefreshTokens] ALTER COLUMN [Token] nvarchar(512) NULL;

        IF COL_LENGTH(N'[dbo].[RefreshTokens]', N'IsRevoked') IS NOT NULL
        BEGIN
            UPDATE [dbo].[RefreshTokens] SET [IsRevoked] = 0 WHERE [IsRevoked] IS NULL;
            ALTER TABLE [dbo].[RefreshTokens] ALTER COLUMN [IsRevoked] bit NOT NULL;
        END

        IF OBJECT_ID(N'[dbo].[PK_RefreshTokens]', N'PK') IS NULL AND COL_LENGTH(N'[dbo].[RefreshTokens]', N'Id') IS NOT NULL
            ALTER TABLE [dbo].[RefreshTokens] ADD CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]);

        IF OBJECT_ID(N'[dbo].[FK_RefreshTokens_AspNetUsers_UserId]', N'F') IS NULL
           AND COL_LENGTH(N'[dbo].[RefreshTokens]', N'UserId') IS NOT NULL
           AND OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
            ALTER TABLE [dbo].[RefreshTokens] ADD CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId]
                FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshToken_Token' AND object_id = OBJECT_ID(N'[dbo].[RefreshTokens]'))
            CREATE UNIQUE INDEX [IX_RefreshToken_Token] ON [dbo].[RefreshTokens] ([Token]) WHERE [Token] IS NOT NULL;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525075829_SyncPendingModelChanges_May25'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525075829_SyncPendingModelChanges_May25', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527175554_AddUserCommunicationProfile'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [EmailOptInJson] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527175554_AddUserCommunicationProfile'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [EscalationEmailsJson] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527175554_AddUserCommunicationProfile'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [NotificationEmailsJson] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527175554_AddUserCommunicationProfile'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PreferredLocale] nvarchar(10) NOT NULL DEFAULT N'ar';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527175554_AddUserCommunicationProfile'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ReplyTo] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527175554_AddUserCommunicationProfile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527175554_AddUserCommunicationProfile', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527220554_AddVendorStaffInvitations'
)
BEGIN
    CREATE TABLE [VendorStaffInvitations] (
        [Id] uniqueidentifier NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [AcceptedUserId] uniqueidentifier NULL,
        [Type] nvarchar(32) NOT NULL,
        [TargetName] nvarchar(200) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [RoleTemplate] nvarchar(64) NOT NULL,
        [BranchIdsJson] nvarchar(max) NOT NULL,
        [PermissionsJson] nvarchar(max) NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [InviteMessage] nvarchar(1000) NULL,
        [SentAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [AcceptedAtUtc] datetime2 NULL,
        [RevokedAtUtc] datetime2 NULL,
        [SendAttemptCount] int NOT NULL,
        [ProviderMessageId] nvarchar(200) NULL,
        [LastSendFailureReason] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_VendorStaffInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VendorStaffInvitations_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527220554_AddVendorStaffInvitations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorStaffInvitations_TokenHash] ON [VendorStaffInvitations] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527220554_AddVendorStaffInvitations'
)
BEGIN
    CREATE INDEX [IX_VendorStaffInvitations_VendorId_Email_Status] ON [VendorStaffInvitations] ([VendorId], [Email], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527220554_AddVendorStaffInvitations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527220554_AddVendorStaffInvitations', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528125039_StrengthenVendorBranchAndStaffManagement'
)
BEGIN
    ALTER TABLE [VendorBranch] ADD [City] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528125039_StrengthenVendorBranchAndStaffManagement'
)
BEGIN
    ALTER TABLE [VendorBranch] ADD [Code] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528125039_StrengthenVendorBranchAndStaffManagement'
)
BEGIN
    ALTER TABLE [VendorBranch] ADD [IsPrimary] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528125039_StrengthenVendorBranchAndStaffManagement'
)
BEGIN
    ALTER TABLE [VendorBranch] ADD [ManagerContact] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528125039_StrengthenVendorBranchAndStaffManagement'
)
BEGIN
    ALTER TABLE [VendorBranch] ADD [ManagerName] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528125039_StrengthenVendorBranchAndStaffManagement'
)
BEGIN
    ALTER TABLE [VendorBranch] ADD [Region] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528125039_StrengthenVendorBranchAndStaffManagement'
)
BEGIN
    ;WITH BranchSequence AS (
        SELECT
            Id,
            VendorId,
            ROW_NUMBER() OVER (PARTITION BY VendorId ORDER BY CreatedAtUtc, Id) AS RowNumber
        FROM VendorBranch
    )
    UPDATE vb
    SET
        Code = CONCAT('BR-', RIGHT(CONCAT('000', CAST(bs.RowNumber AS varchar(10))), 3)),
        IsPrimary = CASE WHEN bs.RowNumber = 1 THEN 1 ELSE 0 END,
        Region = COALESCE(NULLIF(Region, ''), ''),
        City = COALESCE(NULLIF(City, ''), ''),
        ManagerName = COALESCE(NULLIF(ManagerName, ''), Name),
        ManagerContact = COALESCE(NULLIF(ManagerContact, ''), ContactPhone)
    FROM VendorBranch vb
    INNER JOIN BranchSequence bs ON bs.Id = vb.Id;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528125039_StrengthenVendorBranchAndStaffManagement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VendorBranch_VendorId_Code] ON [VendorBranch] ([VendorId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528125039_StrengthenVendorBranchAndStaffManagement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260528125039_StrengthenVendorBranchAndStaffManagement', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528235544_AddAccessApprovalRequests'
)
BEGIN
    CREATE TABLE [AccessApprovalRequests] (
        [Id] uniqueidentifier NOT NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [TargetUserId] uniqueidentifier NULL,
        [Action] nvarchar(100) NOT NULL,
        [Summary] nvarchar(500) NOT NULL,
        [PayloadHash] nvarchar(128) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [DecidedByUserId] uniqueidentifier NULL,
        [DecidedAtUtc] datetime2 NULL,
        [DecisionNote] nvarchar(500) NULL,
        [ConsumedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_AccessApprovalRequests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528235544_AddAccessApprovalRequests'
)
BEGIN
    CREATE INDEX [IX_AccessApprovalRequests_RequestedByUserId_Action_PayloadHash_Status] ON [AccessApprovalRequests] ([RequestedByUserId], [Action], [PayloadHash], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528235544_AddAccessApprovalRequests'
)
BEGIN
    CREATE INDEX [IX_AccessApprovalRequests_Status_CreatedAtUtc] ON [AccessApprovalRequests] ([Status], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528235544_AddAccessApprovalRequests'
)
BEGIN
    CREATE INDEX [IX_AccessApprovalRequests_TargetUserId] ON [AccessApprovalRequests] ([TargetUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260528235544_AddAccessApprovalRequests'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260528235544_AddAccessApprovalRequests', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601000000_RepairMissingCriticalTables'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[DeliveryAssignments]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[DeliveryAssignments]
        (
            [Id] uniqueidentifier NOT NULL,
            [OrderId] uniqueidentifier NOT NULL,
            [DriverId] uniqueidentifier NULL,
            [Status] nvarchar(50) NOT NULL,
            [OfferedAtUtc] datetime2 NULL,
            [OfferExpiresAtUtc] datetime2 NULL,
            [OfferRejectedAtUtc] datetime2 NULL,
            [OfferRejectedReason] nvarchar(100) NULL,
            [DispatchAttemptNumber] int NOT NULL,
            [AcceptedAtUtc] datetime2 NULL,
            [ArrivedAtVendorAtUtc] datetime2 NULL,
            [PickedUpAtUtc] datetime2 NULL,
            [ArrivedAtCustomerAtUtc] datetime2 NULL,
            [DeliveredAtUtc] datetime2 NULL,
            [FailedAtUtc] datetime2 NULL,
            [FailureReason] nvarchar(300) NULL,
            [CodAmount] decimal(18,2) NOT NULL
                CONSTRAINT [DF_DeliveryAssignments_CodAmount] DEFAULT (0),
            [PickupOtpCode] nvarchar(10) NULL,
            [PickupOtpExpiresAtUtc] datetime2 NULL,
            [PickupOtpVerifiedAtUtc] datetime2 NULL,
            [PickupOtpVerifiedByDriverId] uniqueidentifier NULL,
            [DeliveryOtpCode] nvarchar(10) NULL,
            [DeliveryOtpExpiresAtUtc] datetime2 NULL,
            [DeliveryOtpVerifiedAtUtc] datetime2 NULL,
            [DeliveryOtpVerifiedByDriverId] uniqueidentifier NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_DeliveryAssignments] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_DeliveryAssignments_Drivers_DriverId]
                FOREIGN KEY ([DriverId]) REFERENCES [dbo].[Drivers] ([Id]),
            CONSTRAINT [FK_DeliveryAssignments_Orders_OrderId]
                FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id])
        );

        CREATE INDEX [IX_DeliveryAssignments_DriverId]
            ON [dbo].[DeliveryAssignments] ([DriverId]);
        CREATE INDEX [IX_DeliveryAssignments_OrderId]
            ON [dbo].[DeliveryAssignments] ([OrderId]);
    END;

    IF OBJECT_ID(N'[dbo].[AdminAlertEvents]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[AdminAlertEvents]
        (
            [Id] uniqueidentifier NOT NULL,
            [Type] nvarchar(100) NOT NULL,
            [Category] nvarchar(50) NOT NULL,
            [Priority] nvarchar(20) NOT NULL,
            [TitleAr] nvarchar(200) NOT NULL,
            [TitleEn] nvarchar(200) NOT NULL,
            [BodyAr] nvarchar(1000) NOT NULL,
            [BodyEn] nvarchar(1000) NOT NULL,
            [ReferenceId] uniqueidentifier NULL,
            [TargetUrl] nvarchar(500) NOT NULL,
            [DataJson] nvarchar(4000) NOT NULL,
            [DedupeKey] nvarchar(300) NOT NULL,
            [SuppressPush] bit NOT NULL,
            [Status] nvarchar(30) NOT NULL,
            [Attempts] int NOT NULL,
            [NextAttemptAtUtc] datetime2 NULL,
            [LastAttemptAtUtc] datetime2 NULL,
            [CompletedAtUtc] datetime2 NULL,
            [LastError] nvarchar(2000) NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_AdminAlertEvents] PRIMARY KEY ([Id])
        );

        CREATE INDEX [IX_AdminAlertEvents_DedupeKey_CreatedAtUtc]
            ON [dbo].[AdminAlertEvents] ([DedupeKey], [CreatedAtUtc]);
        CREATE INDEX [IX_AdminAlertEvents_Status_NextAttemptAtUtc_CreatedAtUtc]
            ON [dbo].[AdminAlertEvents] ([Status], [NextAttemptAtUtc], [CreatedAtUtc]);
        CREATE INDEX [IX_AdminAlertEvents_Type_ReferenceId_CreatedAtUtc]
            ON [dbo].[AdminAlertEvents] ([Type], [ReferenceId], [CreatedAtUtc]);
    END;

    IF OBJECT_ID(N'[dbo].[AdminAlertDispatches]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[AdminAlertDispatches]
        (
            [Id] uniqueidentifier NOT NULL,
            [AdminAlertEventId] uniqueidentifier NOT NULL,
            [AdminUserId] uniqueidentifier NOT NULL,
            [NotificationId] uniqueidentifier NULL,
            [Status] nvarchar(30) NOT NULL,
            [SignalRSent] bit NOT NULL,
            [PushAttempted] bit NOT NULL,
            [PushSent] bit NOT NULL,
            [PushSkipped] bit NOT NULL,
            [Attempts] int NOT NULL,
            [LastError] nvarchar(1000) NULL,
            [LastAttemptAtUtc] datetime2 NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_AdminAlertDispatches] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_AdminAlertDispatches_AdminAlertEvents_AdminAlertEventId]
                FOREIGN KEY ([AdminAlertEventId])
                REFERENCES [dbo].[AdminAlertEvents] ([Id])
                ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX [IX_AdminAlertDispatches_AdminAlertEventId_AdminUserId]
            ON [dbo].[AdminAlertDispatches] ([AdminAlertEventId], [AdminUserId]);
        CREATE INDEX [IX_AdminAlertDispatches_AdminUserId_CreatedAtUtc]
            ON [dbo].[AdminAlertDispatches] ([AdminUserId], [CreatedAtUtc]);
        CREATE INDEX [IX_AdminAlertDispatches_NotificationId]
            ON [dbo].[AdminAlertDispatches] ([NotificationId]);
    END;

    IF OBJECT_ID(N'[dbo].[Payments]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[Payments]
        (
            [Id] uniqueidentifier NOT NULL,
            [OrderId] uniqueidentifier NOT NULL,
            [Method] nvarchar(50) NOT NULL,
            [Status] nvarchar(50) NOT NULL,
            [ProviderName] nvarchar(100) NULL,
            [ProviderTransactionId] nvarchar(200) NULL,
            [CheckoutDeviceId] nvarchar(200) NULL,
            [Amount] decimal(18,2) NOT NULL,
            [PaidAtUtc] datetime2 NULL,
            [FailedAtUtc] datetime2 NULL,
            [ProviderMethod] nvarchar(40) NULL,
            [ProviderInvoiceId] nvarchar(200) NULL,
            [ProviderStatus] nvarchar(40) NULL,
            [ProviderReferenceNumber] nvarchar(120) NULL,
            [Currency] nvarchar(3) NOT NULL
                CONSTRAINT [DF_Payments_Currency] DEFAULT (N'SAR'),
            [IdempotencyKey] nvarchar(160) NULL,
            [RawCreateResponse] nvarchar(max) NULL,
            [RawFetchResponse] nvarchar(max) NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_Payments_Orders_OrderId]
                FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id])
        );

        CREATE UNIQUE INDEX [IX_Payments_IdempotencyKey]
            ON [dbo].[Payments] ([IdempotencyKey])
            WHERE [IdempotencyKey] IS NOT NULL;
        CREATE INDEX [IX_Payments_OrderId]
            ON [dbo].[Payments] ([OrderId]);
        CREATE INDEX [IX_Payments_Provider_Transaction]
            ON [dbo].[Payments] ([ProviderName], [ProviderTransactionId])
            WHERE [ProviderTransactionId] IS NOT NULL;
    END;

    IF OBJECT_ID(N'[dbo].[PaymentProviderEventInbox]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[PaymentProviderEventInbox]
        (
            [Id] uniqueidentifier NOT NULL,
            [ProviderName] nvarchar(40) NOT NULL,
            [ProviderEventId] nvarchar(200) NOT NULL,
            [EventType] nvarchar(120) NOT NULL,
            [ProviderPaymentId] nvarchar(200) NULL,
            [SecretValid] bit NOT NULL,
            [RawPayload] nvarchar(max) NOT NULL,
            [Headers] nvarchar(max) NULL,
            [Status] nvarchar(30) NOT NULL,
            [FailureReason] nvarchar(1000) NULL,
            [ReceivedAtUtc] datetime2 NOT NULL,
            [ProcessingStartedAtUtc] datetime2 NULL,
            [ProcessedAtUtc] datetime2 NULL,
            [ProcessingAttempts] int NOT NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_PaymentProviderEventInbox] PRIMARY KEY ([Id])
        );

        CREATE UNIQUE INDEX [IX_PaymentProviderEventInbox_Provider_EventId]
            ON [dbo].[PaymentProviderEventInbox] ([ProviderName], [ProviderEventId]);
        CREATE INDEX [IX_PaymentProviderEventInbox_Provider_PaymentId]
            ON [dbo].[PaymentProviderEventInbox] ([ProviderName], [ProviderPaymentId]);
        CREATE INDEX [IX_PaymentProviderEventInbox_Status]
            ON [dbo].[PaymentProviderEventInbox] ([Status]);
    END;

    IF OBJECT_ID(N'[dbo].[OrderSupportCases]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[OrderSupportCases]
        (
            [Id] uniqueidentifier NOT NULL,
            [OrderId] uniqueidentifier NULL,
            [DriverId] uniqueidentifier NULL,
            [CustomerUserId] uniqueidentifier NOT NULL,
            [Type] nvarchar(50) NOT NULL,
            [Status] nvarchar(50) NOT NULL,
            [Priority] nvarchar(50) NOT NULL,
            [Queue] nvarchar(50) NOT NULL,
            [AssignedAdminId] uniqueidentifier NULL,
            [AssignedAtUtc] datetime2 NULL,
            [SlaDueAtUtc] datetime2 NULL,
            [ReasonCode] nvarchar(100) NULL,
            [Message] nvarchar(2000) NOT NULL,
            [DecisionNotes] nvarchar(2000) NULL,
            [CustomerVisibleNote] nvarchar(2000) NULL,
            [RequestedRefundAmount] decimal(18,2) NULL,
            [ApprovedRefundAmount] decimal(18,2) NULL,
            [RefundMethod] nvarchar(50) NULL,
            [CompensationType] int NULL,
            [CompensationCouponId] uniqueidentifier NULL,
            [CostBearer] nvarchar(50) NULL,
            [ClosedAtUtc] datetime2 NULL,
            [InitiatorRole] nvarchar(20) NOT NULL
                CONSTRAINT [DF_OrderSupportCases_InitiatorRole] DEFAULT (N'customer'),
            [VendorResponse] nvarchar(2000) NULL,
            [VendorRespondedAtUtc] datetime2 NULL,
            [DriverResponse] nvarchar(2000) NULL,
            [DriverRespondedAtUtc] datetime2 NULL,
            [ResolutionCode] nvarchar(100) NULL,
            [AwaitingResponseFromRole] nvarchar(20) NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_OrderSupportCases] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_OrderSupportCases_Orders_OrderId]
                FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([Id]) ON DELETE CASCADE
        );

        CREATE INDEX [IX_OrderSupportCases_DriverId_Type_Status]
            ON [dbo].[OrderSupportCases] ([DriverId], [Type], [Status]);
        CREATE INDEX [IX_OrderSupportCases_OrderId_Status]
            ON [dbo].[OrderSupportCases] ([OrderId], [Status]);
    END;

    IF OBJECT_ID(N'[dbo].[OrderSupportCaseActivities]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[OrderSupportCaseActivities]
        (
            [Id] uniqueidentifier NOT NULL,
            [OrderSupportCaseId] uniqueidentifier NOT NULL,
            [Action] nvarchar(50) NOT NULL,
            [Title] nvarchar(200) NOT NULL,
            [Note] nvarchar(2000) NULL,
            [ActorUserId] uniqueidentifier NULL,
            [ActorRole] nvarchar(50) NOT NULL,
            [VisibleToCustomer] bit NOT NULL,
            [MessageType] nvarchar(50) NOT NULL
                CONSTRAINT [DF_OrderSupportCaseActivities_MessageType] DEFAULT (N'system'),
            [Audience] nvarchar(100) NOT NULL
                CONSTRAINT [DF_OrderSupportCaseActivities_Audience] DEFAULT (N'all_external'),
            [IsInternalOnly] bit NOT NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_OrderSupportCaseActivities] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_OrderSupportCaseActivities_OrderSupportCases_OrderSupportCaseId]
                FOREIGN KEY ([OrderSupportCaseId])
                REFERENCES [dbo].[OrderSupportCases] ([Id])
                ON DELETE CASCADE
        );

        CREATE INDEX [IX_OrderSupportCaseActivities_OrderSupportCaseId]
            ON [dbo].[OrderSupportCaseActivities] ([OrderSupportCaseId]);
    END;

    IF OBJECT_ID(N'[dbo].[OrderSupportCaseAttachments]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[OrderSupportCaseAttachments]
        (
            [Id] uniqueidentifier NOT NULL,
            [OrderSupportCaseId] uniqueidentifier NOT NULL,
            [FileName] nvarchar(255) NOT NULL,
            [FileUrl] nvarchar(2000) NOT NULL,
            [UploadedByUserId] uniqueidentifier NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_OrderSupportCaseAttachments] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_OrderSupportCaseAttachments_OrderSupportCases_OrderSupportCaseId]
                FOREIGN KEY ([OrderSupportCaseId])
                REFERENCES [dbo].[OrderSupportCases] ([Id])
                ON DELETE CASCADE
        );

        CREATE INDEX [IX_OrderSupportCaseAttachments_OrderSupportCaseId]
            ON [dbo].[OrderSupportCaseAttachments] ([OrderSupportCaseId]);
    END;

    IF OBJECT_ID(N'[dbo].[PlatformBankAccounts]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[PlatformBankAccounts]
        (
            [Id] uniqueidentifier NOT NULL,
            [BankName] nvarchar(200) NOT NULL,
            [AccountHolderName] nvarchar(200) NOT NULL,
            [IBAN] nvarchar(34) NOT NULL,
            [AccountNumber] nvarchar(64) NULL,
            [CountryCode] nvarchar(2) NOT NULL
                CONSTRAINT [DF_PlatformBankAccounts_CountryCode] DEFAULT (N'SA'),
            [City] nvarchar(100) NOT NULL
                CONSTRAINT [DF_PlatformBankAccounts_City] DEFAULT (N'Riyadh'),
            [IsActive] bit NOT NULL
                CONSTRAINT [DF_PlatformBankAccounts_IsActive] DEFAULT (1),
            [IsBankTransferEnabled] bit NOT NULL
                CONSTRAINT [DF_PlatformBankAccounts_IsBankTransferEnabled] DEFAULT (1),
            [IsMoyasarPayoutsEnabled] bit NOT NULL
                CONSTRAINT [DF_PlatformBankAccounts_IsMoyasarPayoutsEnabled] DEFAULT (0),
            [MoyasarPayoutSourceId] nvarchar(100) NULL,
            [Notes] nvarchar(500) NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [UpdatedAtUtc] datetime2 NOT NULL,
            CONSTRAINT [PK_PlatformBankAccounts] PRIMARY KEY ([Id])
        );

        CREATE INDEX [IX_PlatformBankAccounts_IsActive]
            ON [dbo].[PlatformBankAccounts] ([IsActive]);
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260601000000_RepairMissingCriticalTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260601000000_RepairMissingCriticalTables', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    DROP INDEX [IX_OrderStatusHistories_OrderId] ON [OrderStatusHistories];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    DROP INDEX [IX_Orders_UserId] ON [Orders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    DROP INDEX [IX_Orders_VendorId] ON [Orders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    DROP INDEX [IX_DeliveryAssignments_DriverId] ON [DeliveryAssignments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    DROP INDEX [IX_DeliveryAssignments_OrderId] ON [DeliveryAssignments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    DROP INDEX [IX_CustomerAddresses_UserId] ON [CustomerAddresses];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_OrderId_CreatedAt_Desc] ON [OrderStatusHistories] ([OrderId], [CreatedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_Orders_PaymentStatus_PlacedAt_Desc] ON [Orders] ([PaymentStatus], [PlacedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_Orders_Status_PlacedAt_Desc] ON [Orders] ([Status], [PlacedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId_PlacedAt_Desc] ON [Orders] ([UserId], [PlacedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_Orders_UserId_Status_PlacedAt_Desc] ON [Orders] ([UserId], [Status], [PlacedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_Orders_VendorId_BranchId_PlacedAt_Desc] ON [Orders] ([VendorId], [VendorBranchId], [PlacedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_Orders_VendorId_PlacedAt_Desc] ON [Orders] ([VendorId], [PlacedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_Orders_VendorId_Status_PlacedAt_Desc] ON [Orders] ([VendorId], [Status], [PlacedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_DeliveryAssignments_DriverId_Status_CreatedAt_Desc] ON [DeliveryAssignments] ([DriverId], [Status], [CreatedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_DeliveryAssignments_OrderId_CreatedAt_Desc] ON [DeliveryAssignments] ([OrderId], [CreatedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_DeliveryAssignments_OrderId_Status_CreatedAt_Desc] ON [DeliveryAssignments] ([OrderId], [Status], [CreatedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    CREATE INDEX [IX_CustomerAddresses_UserId_Default_Updated_Created_Desc] ON [CustomerAddresses] ([UserId], [IsDefault] DESC, [UpdatedAtUtc] DESC, [CreatedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603125508_AddHotPathIndexesForLoad'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603125508_AddHotPathIndexesForLoad', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603221340_EnsureOrderItemSnapshotColumns'
)
BEGIN
    IF COL_LENGTH('dbo.OrderItems', 'SnapshotImageUrl') IS NULL
        ALTER TABLE [dbo].[OrderItems] ADD [SnapshotImageUrl] nvarchar(2048) NULL;

    IF COL_LENGTH('dbo.OrderItems', 'SnapshotDisplaySize') IS NULL
        ALTER TABLE [dbo].[OrderItems] ADD [SnapshotDisplaySize] nvarchar(200) NULL;

    IF COL_LENGTH('dbo.OrderItems', 'SnapshotBarcode') IS NULL
        ALTER TABLE [dbo].[OrderItems] ADD [SnapshotBarcode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603221340_EnsureOrderItemSnapshotColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603221340_EnsureOrderItemSnapshotColumns', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'RIYADH')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000001', N'RIYADH', N'منطقة الرياض', N'Riyadh Region', 24.7136, 46.6753, 8, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'MAKKAH')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000002', N'MAKKAH', N'منطقة مكة المكرمة', N'Makkah Region', 21.4225, 39.8262, 8, 2, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'MADINAH')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000003', N'MADINAH', N'منطقة المدينة المنورة', N'Madinah Region', 24.4672, 39.6024, 8, 3, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'EASTERN')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000004', N'EASTERN', N'المنطقة الشرقية', N'Eastern Region', 26.3927, 49.9777, 7, 4, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'QASSIM')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000005', N'QASSIM', N'منطقة القصيم', N'Qassim Region', 26.3267, 43.965, 8, 5, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'HAIL')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000006', N'HAIL', N'منطقة حائل', N'Hail Region', 27.5114, 41.7208, 8, 6, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'TABUK')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000007', N'TABUK', N'منطقة تبوك', N'Tabuk Region', 28.3835, 36.5662, 7, 7, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'NORTHERN_BORDERS')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000008', N'NORTHERN_BORDERS', N'منطقة الحدود الشمالية', N'Northern Borders', 30.9753, 41.0186, 7, 8, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'JAWF')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000009', N'JAWF', N'منطقة الجوف', N'Al Jawf Region', 29.9697, 40.2064, 8, 9, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'JIZAN')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000010', N'JIZAN', N'منطقة جازان', N'Jizan Region', 16.8893, 42.551, 9, 10, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'ASIR')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000011', N'ASIR', N'منطقة عسير', N'Asir Region', 18.2164, 42.5053, 8, 11, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'BAHA')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000012', N'BAHA', N'منطقة الباحة', N'Al Baha Region', 20, 41.4667, 9, 12, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiRegions] WHERE [Code] = N'NAJRAN')
    BEGIN
        INSERT INTO [SaudiRegions] ([Id], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES ('10000000-0000-0000-0000-000000000013', N'NAJRAN', N'منطقة نجران', N'Najran Region', 17.4933, 44.1322, 8, 13, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'RIYADH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'RIYADH', N'الرياض', N'Riyadh', 24.7136, 46.6753, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'DIRIYAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'DIRIYAH', N'الدرعية', N'Diriyah', 24.7136, 46.6753, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'KHARJ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'KHARJ', N'الخرج', N'Al Kharj', 24.15, 47.3, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'DAWADMI')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'DAWADMI', N'الدوادمي', N'Ad Dawadmi', 24.7136, 46.6753, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'MAJMAAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'MAJMAAH', N'المجمعة', N'Al Majmaah', 24.7136, 46.6753, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'WADI_DAWASIR')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'WADI_DAWASIR', N'وادي الدواسر', N'Wadi ad-Dawasir', 24.7136, 46.6753, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AFIF')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AFIF', N'عفيف', N'Afif', 24.7136, 46.6753, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'SHAQRA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'SHAQRA', N'شقراء', N'Shaqqra', 24.7136, 46.6753, 12, 8, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AZ_ZULFI')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AZ_ZULFI', N'الزلفي', N'Az Zulfi', 24.7136, 46.6753, 12, 9, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AS_SULAYYIL')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AS_SULAYYIL', N'السليل', N'As Sulayyil', 24.7136, 46.6753, 12, 10, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_QUWAYIYAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_QUWAYIYAH', N'القويعية', N'Al Quwayiyah', 24.7136, 46.6753, 12, 11, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'HOTAT_BANI_TAMIM')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'HOTAT_BANI_TAMIM', N'حوطة بني تميم', N'Hotat Bani Tamim', 24.7136, 46.6753, 12, 12, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_AFLAJ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_AFLAJ', N'الأفلاج', N'Al Aflaj', 24.7136, 46.6753, 12, 13, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_GHAT')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_GHAT', N'الغاط', N'Al Ghat', 24.7136, 46.6753, 12, 14, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_HARIQ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_HARIQ', N'الحريق', N'Al Hariq', 24.7136, 46.6753, 12, 15, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'HURAYMILA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'HURAYMILA', N'حريملاء', N'Huraymila', 24.7136, 46.6753, 12, 16, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'RUMAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'RUMAH', N'رماح', N'Rumah', 24.7136, 46.6753, 12, 17, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'THADIQ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'THADIQ', N'ثادق', N'Thadiq', 24.7136, 46.6753, 12, 18, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'DURMA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'DURMA', N'ضرما', N'Durma', 24.7136, 46.6753, 12, 19, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'MARAT')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'MARAT', N'مرات', N'Marat', 24.7136, 46.6753, 12, 20, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'RIYADH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'MAKKAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'MAKKAH', N'مكة المكرمة', N'Makkah', 21.4225, 39.8262, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'JEDDAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'JEDDAH', N'جدة', N'Jeddah', 21.5433, 39.1728, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'TAIF')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'TAIF', N'الطائف', N'Taif', 21.4225, 39.8262, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'RABIGH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'RABIGH', N'رابغ', N'Rabigh', 21.4225, 39.8262, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'QUNFUDHAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'QUNFUDHAH', N'القنفذة', N'Al Qunfudhah', 21.4225, 39.8262, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_LITH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_LITH', N'الليث', N'Al Lith', 21.4225, 39.8262, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_JUMUM')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_JUMUM', N'الجموم', N'Al Jumum', 21.4225, 39.8262, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_KAMIL')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_KAMIL', N'الكامل', N'Al Kamil', 21.4225, 39.8262, 12, 8, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BAHRAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BAHRAH', N'بحرة', N'Bahrah', 21.4225, 39.8262, 12, 9, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'ADHAM')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'ADHAM', N'أضم', N'Adham', 21.4225, 39.8262, 12, 10, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'KHURMA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'KHURMA', N'الخرمة', N'Khurma', 21.4225, 39.8262, 12, 11, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'RANYAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'RANYAH', N'رنية', N'Ranyah', 21.4225, 39.8262, 12, 12, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'TURBAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'TURBAH', N'تربة', N'Turbah', 21.4225, 39.8262, 12, 13, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'KHULAIS')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'KHULAIS', N'خليص', N'Khulais', 21.4225, 39.8262, 12, 14, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MAKKAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'MADINAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'MADINAH', N'المدينة المنورة', N'Madinah', 24.4672, 39.6024, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MADINAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'YANBU')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'YANBU', N'ينبع', N'Yanbu', 24.4672, 39.6024, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MADINAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'ULA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'ULA', N'العلا', N'Al Ula', 24.4672, 39.6024, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MADINAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BADR')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BADR', N'بدر', N'Badr', 24.4672, 39.6024, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MADINAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_HENAKIYAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_HENAKIYAH', N'الحناكية', N'Al Henakiyah', 24.4672, 39.6024, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MADINAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'MAHD_ADH_DHAHAB')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'MAHD_ADH_DHAHAB', N'مهد الذهب', N'Mahd adh Dhahab', 24.4672, 39.6024, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MADINAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'KHAYBAR')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'KHAYBAR', N'خيبر', N'Khaybar', 24.4672, 39.6024, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'MADINAH';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'DAMMAM')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'DAMMAM', N'الدمام', N'Dammam', 26.3927, 49.9777, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'KHOBAR')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'KHOBAR', N'الخبر', N'Al Khobar', 26.3927, 49.9777, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'DHAHRAN')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'DHAHRAN', N'الظهران', N'Dhahran', 26.3927, 49.9777, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'JUBAIL')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'JUBAIL', N'الجبيل', N'Jubail', 26.3927, 49.9777, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'QATIF')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'QATIF', N'القطيف', N'Qatif', 26.3927, 49.9777, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'HOFUF')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'HOFUF', N'الهفوف', N'Al Hofuf', 26.3927, 49.9777, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'MUBARRAZ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'MUBARRAZ', N'المبرز', N'Al Mubarraz', 26.3927, 49.9777, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'KHAFJI')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'KHAFJI', N'الخفجي', N'Khafji', 26.3927, 49.9777, 12, 8, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'HAFR_AL_BATIN')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'HAFR_AL_BATIN', N'حفر الباطن', N'Hafar Al Batin', 26.3927, 49.9777, 12, 9, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'RAS_TANURA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'RAS_TANURA', N'رأس تنورة', N'Ras Tanura', 26.3927, 49.9777, 12, 10, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'ABQAIQ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'ABQAIQ', N'بقيق', N'Abqaiq', 26.3927, 49.9777, 12, 11, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'NAIRYAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'NAIRYAH', N'النعيرية', N'Nairyah', 26.3927, 49.9777, 12, 12, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'SAIHAT')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'SAIHAT', N'سيهات', N'Saihat', 26.3927, 49.9777, 12, 13, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'TARUT')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'TARUT', N'تاروت', N'Tarut', 26.3927, 49.9777, 12, 14, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'SAFWA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'SAFWA', N'صفوى', N'Safwa', 26.3927, 49.9777, 12, 15, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AWAMIYAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AWAMIYAH', N'العوامية', N'Awamiyah', 26.3927, 49.9777, 12, 16, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'RAHIMAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'RAHIMAH', N'رحيمة', N'Rahimah', 26.3927, 49.9777, 12, 17, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'EASTERN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BURAYDAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BURAYDAH', N'بريدة', N'Buraydah', 26.3267, 43.965, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'QASSIM';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'UNAYZAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'UNAYZAH', N'عنيزة', N'Unayzah', 26.3267, 43.965, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'QASSIM';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'RASS')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'RASS', N'الرس', N'Ar Rass', 26.3267, 43.965, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'QASSIM';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_MITHNAB')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_MITHNAB', N'المذنب', N'Al Mithnab', 26.3267, 43.965, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'QASSIM';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_BADAYEA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_BADAYEA', N'البدائع', N'Al Badayea', 26.3267, 43.965, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'QASSIM';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_ASYAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_ASYAH', N'الأسياح', N'Al Asyah', 26.3267, 43.965, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'QASSIM';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'UYUN_AL_JIWA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'UYUN_AL_JIWA', N'عيون الجواء', N'Uyun Al Jiwa', 26.3267, 43.965, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'QASSIM';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'HAIL_CITY')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'HAIL_CITY', N'حائل', N'Hail', 27.5114, 41.7208, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'HAIL';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BAQAA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BAQAA', N'بقعاء', N'Baqaa', 27.5114, 41.7208, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'HAIL';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'ASH_SHAMLI')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'ASH_SHAMLI', N'الشملي', N'Ash Shamli', 27.5114, 41.7208, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'HAIL';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'MOQAQ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'MOQAQ', N'موقق', N'Moqaq', 27.5114, 41.7208, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'HAIL';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'TABUK_CITY')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'TABUK_CITY', N'تبوك', N'Tabuk', 28.3835, 36.5662, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'TABUK';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'WAJH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'WAJH', N'الوجه', N'Al Wajh', 28.3835, 36.5662, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'TABUK';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'DUBA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'DUBA', N'ضباء', N'Duba', 28.3835, 36.5662, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'TABUK';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'NEOM')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'NEOM', N'نيوم', N'NEOM', 28.3835, 36.5662, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'TABUK';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'HAQL')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'HAQL', N'حقل', N'Haql', 28.3835, 36.5662, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'TABUK';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'UMLUJ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'UMLUJ', N'أملج', N'Umluj', 28.3835, 36.5662, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'TABUK';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'TAYMA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'TAYMA', N'تيماء', N'Tayma', 28.3835, 36.5662, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'TABUK';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_BAD')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_BAD', N'البدع', N'Al Bad', 28.3835, 36.5662, 12, 8, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'TABUK';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'ARAR')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'ARAR', N'عرعر', N'Arar', 30.9753, 41.0186, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'NORTHERN_BORDERS';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'RAFHA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'RAFHA', N'رفحاء', N'Rafha', 30.9753, 41.0186, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'NORTHERN_BORDERS';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'TURAIF')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'TURAIF', N'طريف', N'Turaif', 30.9753, 41.0186, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'NORTHERN_BORDERS';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_UWAYQILAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_UWAYQILAH', N'العويقيلة', N'Al Uwayqilah', 30.9753, 41.0186, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'NORTHERN_BORDERS';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'SAKAKA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'SAKAKA', N'سكاكا', N'Sakaka', 29.9697, 40.2064, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JAWF';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'DUMAT_JANDAL')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'DUMAT_JANDAL', N'دومة الجندل', N'Dumat Al-Jandal', 29.9697, 40.2064, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JAWF';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'QURAYAT')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'QURAYAT', N'القريات', N'Qurayat', 29.9697, 40.2064, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JAWF';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'TABARJAL')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'TABARJAL', N'طبرجل', N'Tabarjal', 29.9697, 40.2064, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JAWF';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'JIZAN_CITY')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'JIZAN_CITY', N'جازان', N'Jizan', 16.8893, 42.551, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JIZAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'SABYA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'SABYA', N'صبيا', N'Sabya', 16.8893, 42.551, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JIZAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'ABU_ARISH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'ABU_ARISH', N'أبو عريش', N'Abu Arish', 16.8893, 42.551, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JIZAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'SAMTAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'SAMTAH', N'صامطة', N'Samtah', 16.8893, 42.551, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JIZAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'FAYFA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'FAYFA', N'فيفاء', N'Fayfa', 16.8893, 42.551, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JIZAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_DAYER')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_DAYER', N'الدائر', N'Al Dayer', 16.8893, 42.551, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JIZAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BAISH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BAISH', N'بيش', N'Baish', 16.8893, 42.551, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JIZAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'DAMAD')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'DAMAD', N'ضمد', N'Damad', 16.8893, 42.551, 12, 8, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JIZAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_ARIDAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_ARIDAH', N'العارضة', N'Al Aridah', 16.8893, 42.551, 12, 9, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'JIZAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'ABHA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'ABHA', N'أبها', N'Abha', 18.2164, 42.5053, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'ASIR';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'KHAMIS_MUSHAIT')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'KHAMIS_MUSHAIT', N'خميس مشيط', N'Khamis Mushait', 18.2164, 42.5053, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'ASIR';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BISHA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BISHA', N'بيشة', N'Bisha', 18.2164, 42.5053, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'ASIR';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'NAMAS')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'NAMAS', N'النماص', N'An Namas', 18.2164, 42.5053, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'ASIR';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'MAHAYIL')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'MAHAYIL', N'محايل', N'Mahayil', 18.2164, 42.5053, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'ASIR';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'SARAT_ABIDAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'SARAT_ABIDAH', N'سراة عبيدة', N'Sarat Abidah', 18.2164, 42.5053, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'ASIR';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'TATHLEETH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'TATHLEETH', N'تثليث', N'Tathleeth', 18.2164, 42.5053, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'ASIR';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BALLASMAR')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BALLASMAR', N'بللسمر', N'Ballasmar', 18.2164, 42.5053, 12, 8, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'ASIR';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BAREQ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BAREQ', N'بارق', N'Bareq', 18.2164, 42.5053, 12, 9, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'ASIR';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BAHA_CITY')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BAHA_CITY', N'الباحة', N'Al Baha', 20, 41.4667, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'BAHA';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'BALJURASHI')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'BALJURASHI', N'بلجرشي', N'Baljurashi', 20, 41.4667, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'BAHA';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_MIKHWAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_MIKHWAH', N'المخواة', N'Al Mikhwah', 20, 41.4667, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'BAHA';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_MANDAQ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_MANDAQ', N'المندق', N'Al Mandaq', 20, 41.4667, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'BAHA';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'QILWAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'QILWAH', N'قلوة', N'Qilwah', 20, 41.4667, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'BAHA';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'AL_AQIQ')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'AL_AQIQ', N'العقيق', N'Al Aqiq', 20, 41.4667, 12, 6, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'BAHA';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'QARA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'QARA', N'القرى', N'Qara', 20, 41.4667, 12, 7, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'BAHA';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'NAJRAN_CITY')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'NAJRAN_CITY', N'نجران', N'Najran', 17.4933, 44.1322, 12, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'NAJRAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'SHARURAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'SHARURAH', N'شرورة', N'Sharurah', 17.4933, 44.1322, 12, 2, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'NAJRAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'HUBUNA')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'HUBUNA', N'حبونا', N'Hubuna', 17.4933, 44.1322, 12, 3, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'NAJRAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'YADAMAH')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'YADAMAH', N'يدمة', N'Yadamah', 17.4933, 44.1322, 12, 4, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'NAJRAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [SaudiCities] WHERE [Code] = N'THAR')
    BEGIN
        INSERT INTO [SaudiCities] ([Id], [RegionId], [Code], [NameAr], [NameEn], [Latitude], [Longitude], [MapZoom], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
        SELECT NEWID(), [Id], N'THAR', N'ثار', N'Thar', 17.4933, 44.1322, 12, 5, SYSUTCDATETIME(), SYSUTCDATETIME()
        FROM [SaudiRegions]
        WHERE [Code] = N'NAJRAN';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260604164727_ExpandSaudiGeographyCatalog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260604164727_ExpandSaudiGeographyCatalog', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614120000_AllowBranchScopedVendorProductInventory'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614120000_AllowBranchScopedVendorProductInventory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260614120000_AllowBranchScopedVendorProductInventory', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618140305_AlignPiiEncryptionModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618140305_AlignPiiEncryptionModel', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618191843_OptimizeDeliveryDispatchHotPaths'
)
BEGIN
    CREATE INDEX [IX_DeliveryOfferAttempts_OrderId_OfferedAtUtc_Desc] ON [DeliveryOfferAttempts] ([OrderId], [OfferedAtUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618191843_OptimizeDeliveryDispatchHotPaths'
)
BEGIN
    CREATE INDEX [IX_DeliveryAssignments_Status_OfferExpiresAtUtc] ON [DeliveryAssignments] ([Status], [OfferExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618191843_OptimizeDeliveryDispatchHotPaths'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618191843_OptimizeDeliveryDispatchHotPaths', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DROP INDEX [IX_Vendor_CommRegNum] ON [Vendor];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var30 sysname;
    SELECT @var30 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'TaxId');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var30 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [TaxId] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var31 sysname;
    SELECT @var31 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'OwnerPhone');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var31 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [OwnerPhone] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var32 sysname;
    SELECT @var32 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'OwnerName');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var32 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [OwnerName] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var33 sysname;
    SELECT @var33 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'OwnerEmail');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var33 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [OwnerEmail] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var34 sysname;
    SELECT @var34 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'NationalAddress');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var34 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [NationalAddress] nvarchar(2048) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var35 sysname;
    SELECT @var35 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'LicenseNumber');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var35 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [LicenseNumber] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var36 sysname;
    SELECT @var36 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'IdNumber');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var36 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [IdNumber] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var37 sysname;
    SELECT @var37 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'ContactPhone');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var37 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [ContactPhone] nvarchar(512) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var38 sysname;
    SELECT @var38 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'ContactEmail');
    IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var38 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [ContactEmail] nvarchar(512) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    DECLARE @var39 sysname;
    SELECT @var39 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vendor]') AND [c].[name] = N'CommercialRegistrationNumber');
    IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [Vendor] DROP CONSTRAINT [' + @var39 + '];');
    ALTER TABLE [Vendor] ALTER COLUMN [CommercialRegistrationNumber] nvarchar(512) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    ALTER TABLE [Vendor] ADD [CommercialRegistrationNumberHash] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Vendor_CommRegNumHash] ON [Vendor] ([CommercialRegistrationNumberHash]) WHERE [CommercialRegistrationNumberHash] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260622031302_EncryptVendorSensitiveDataAndHardenApproval', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622052650_AddProductRequestSizesAndImages'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [SuggestedImageUrlsJson] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622052650_AddProductRequestSizesAndImages'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [SuggestedMeasurementValue] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622052650_AddProductRequestSizesAndImages'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD [SuggestedPackageTypeId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622052650_AddProductRequestSizesAndImages'
)
BEGIN
    CREATE INDEX [IX_ProductRequest_SuggestedPackageTypeId] ON [ProductRequest] ([SuggestedPackageTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622052650_AddProductRequestSizesAndImages'
)
BEGIN
    ALTER TABLE [ProductRequest] ADD CONSTRAINT [FK_ProductRequest_UnitOfMeasure_SuggestedPackageTypeId] FOREIGN KEY ([SuggestedPackageTypeId]) REFERENCES [UnitOfMeasure] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260622052650_AddProductRequestSizesAndImages'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260622052650_AddProductRequestSizesAndImages', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623121053_AddCategoryNotificationSoundsToUserPushDevices'
)
BEGIN
    ALTER TABLE [UserPushDevices] ADD [CategoryNotificationSoundsJson] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623121053_AddCategoryNotificationSoundsToUserPushDevices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260623121053_AddCategoryNotificationSoundsToUserPushDevices', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709150603_BackfillRefundSupportCasesForAdminQueues'
)
BEGIN
    DECLARE @RefundCaseMap TABLE
    (
        OrderId uniqueidentifier NOT NULL PRIMARY KEY,
        CaseId uniqueidentifier NOT NULL,
        RefundId uniqueidentifier NOT NULL
    );

    INSERT INTO @RefundCaseMap (OrderId, CaseId, RefundId)
    SELECT candidate.OrderId, NEWID(), candidate.RefundId
    FROM
    (
        SELECT
            p.OrderId,
            r.Id AS RefundId,
            ROW_NUMBER() OVER (PARTITION BY p.OrderId ORDER BY r.CreatedAtUtc DESC, r.Id DESC) AS RowNumber
        FROM [Refunds] AS r
        INNER JOIN [Payments] AS p ON p.Id = r.PaymentId
        INNER JOIN [Orders] AS o ON o.Id = p.OrderId
        WHERE r.OrderSupportCaseId IS NULL
          AND NOT EXISTS
          (
              SELECT 1
              FROM [OrderSupportCases] AS existingCase
              WHERE existingCase.OrderId = p.OrderId
                AND existingCase.Type = N'ReturnRequest'
          )
    ) AS candidate
    WHERE candidate.RowNumber = 1;

    INSERT INTO [OrderSupportCases]
    (
        [Id],
        [OrderId],
        [DriverId],
        [CustomerUserId],
        [Type],
        [Status],
        [Priority],
        [Queue],
        [AssignedAdminId],
        [AssignedAtUtc],
        [SlaDueAtUtc],
        [ReasonCode],
        [Message],
        [DecisionNotes],
        [CustomerVisibleNote],
        [RequestedRefundAmount],
        [ApprovedRefundAmount],
        [RefundMethod],
        [CompensationType],
        [CompensationCouponId],
        [CostBearer],
        [ClosedAtUtc],
        [InitiatorRole],
        [VendorResponse],
        [VendorRespondedAtUtc],
        [DriverResponse],
        [DriverRespondedAtUtc],
        [ResolutionCode],
        [AwaitingResponseFromRole],
        [CreatedAtUtc],
        [UpdatedAtUtc]
    )
    SELECT
        map.CaseId,
        p.OrderId,
        NULL,
        o.UserId,
        N'ReturnRequest',
        CASE
            WHEN r.LifecycleStatus IN (N'Failed', N'Cancelled') OR r.Status IN (N'Failed', N'Cancelled') THEN N'Rejected'
            WHEN r.LifecycleStatus IN (N'Requested', N'Processing') THEN N'InReview'
            ELSE N'Approved'
        END,
        N'High',
        N'Finance',
        NULL,
        NULL,
        DATEADD(hour, 8, COALESCE(r.CreatedAtUtc, SYSUTCDATETIME())),
        N'admin_refund_backfill',
        LEFT(CONCAT(
            N'Admin refund recorded.',
            CASE
                WHEN NULLIF(LTRIM(RTRIM(r.Reason)), N'') IS NULL THEN N''
                ELSE CONCAT(N' ', LTRIM(RTRIM(r.Reason)))
            END), 2000),
        LEFT(COALESCE(NULLIF(LTRIM(RTRIM(r.Reason)), N''), N'Backfilled from refund record.'), 2000),
        LEFT(
            CASE
                WHEN r.LifecycleStatus IN (N'Failed', N'Cancelled') OR r.Status IN (N'Failed', N'Cancelled')
                    THEN COALESCE(NULLIF(LTRIM(RTRIM(r.Reason)), N''), N'Refund could not be completed.')
                ELSE COALESCE(NULLIF(LTRIM(RTRIM(r.Reason)), N''), N'Your refund has been processed.')
            END,
            2000),
        CASE WHEN r.RequestedAmount > 0 THEN r.RequestedAmount ELSE r.Amount END,
        CASE WHEN r.ApprovedAmount > 0 THEN r.ApprovedAmount ELSE r.Amount END,
        COALESCE(
            NULLIF(LTRIM(RTRIM(r.RefundMethod)), N''),
            CASE LOWER(r.CompensationMethod)
                WHEN N'coupon' THEN N'coupon'
                WHEN N'manual' THEN N'manual'
                ELSE N'same_method'
            END),
        CASE WHEN LOWER(r.CompensationMethod) = N'coupon' THEN 1 ELSE 0 END,
        NULL,
        COALESCE(NULLIF(LEFT(LTRIM(RTRIM(r.CostBearer)), 50), N''), N'Platform'),
        CASE
            WHEN r.LifecycleStatus IN (N'Failed', N'Cancelled') OR r.Status IN (N'Failed', N'Cancelled')
                THEN COALESCE(r.FailedAtUtc, r.UpdatedAtUtc, r.CreatedAtUtc, SYSUTCDATETIME())
            ELSE NULL
        END,
        N'admin',
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        NULL,
        COALESCE(r.CreatedAtUtc, SYSUTCDATETIME()),
        COALESCE(r.UpdatedAtUtc, r.CreatedAtUtc, SYSUTCDATETIME())
    FROM @RefundCaseMap AS map
    INNER JOIN [Refunds] AS r ON r.Id = map.RefundId
    INNER JOIN [Payments] AS p ON p.Id = r.PaymentId
    INNER JOIN [Orders] AS o ON o.Id = p.OrderId;

    INSERT INTO [OrderSupportCaseActivities]
    (
        [Id],
        [OrderSupportCaseId],
        [Action],
        [Title],
        [Note],
        [ActorUserId],
        [ActorRole],
        [VisibleToCustomer],
        [CreatedAtUtc],
        [UpdatedAtUtc],
        [MessageType],
        [Audience],
        [IsInternalOnly]
    )
    SELECT
        NEWID(),
        map.CaseId,
        N'submitted',
        N'Return request submitted',
        LEFT(COALESCE(NULLIF(LTRIM(RTRIM(r.Reason)), N''), N'Admin refund recorded.'), 2000),
        NULL,
        N'admin',
        0,
        COALESCE(r.CreatedAtUtc, SYSUTCDATETIME()),
        COALESCE(r.CreatedAtUtc, SYSUTCDATETIME()),
        N'case_opened',
        N'internal_admin_only',
        1
    FROM @RefundCaseMap AS map
    INNER JOIN [Refunds] AS r ON r.Id = map.RefundId;

    INSERT INTO [OrderSupportCaseActivities]
    (
        [Id],
        [OrderSupportCaseId],
        [Action],
        [Title],
        [Note],
        [ActorUserId],
        [ActorRole],
        [VisibleToCustomer],
        [CreatedAtUtc],
        [UpdatedAtUtc],
        [MessageType],
        [Audience],
        [IsInternalOnly]
    )
    SELECT
        NEWID(),
        map.CaseId,
        CASE
            WHEN r.LifecycleStatus IN (N'Failed', N'Cancelled') OR r.Status IN (N'Failed', N'Cancelled') THEN N'rejected'
            ELSE N'approved'
        END,
        CASE
            WHEN r.LifecycleStatus IN (N'Failed', N'Cancelled') OR r.Status IN (N'Failed', N'Cancelled') THEN N'Case rejected'
            ELSE N'Case approved'
        END,
        LEFT(COALESCE(NULLIF(LTRIM(RTRIM(r.Reason)), N''), N'Backfilled from refund record.'), 2000),
        NULL,
        N'admin',
        1,
        COALESCE(r.UpdatedAtUtc, r.CreatedAtUtc, SYSUTCDATETIME()),
        COALESCE(r.UpdatedAtUtc, r.CreatedAtUtc, SYSUTCDATETIME()),
        N'decision',
        N'all_external',
        0
    FROM @RefundCaseMap AS map
    INNER JOIN [Refunds] AS r ON r.Id = map.RefundId
    WHERE r.LifecycleStatus NOT IN (N'Requested', N'Processing');

    UPDATE r
    SET r.OrderSupportCaseId = map.CaseId
    FROM [Refunds] AS r
    INNER JOIN [Payments] AS p ON p.Id = r.PaymentId
    INNER JOIN @RefundCaseMap AS map ON map.OrderId = p.OrderId
    WHERE r.OrderSupportCaseId IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709150603_BackfillRefundSupportCasesForAdminQueues'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709150603_BackfillRefundSupportCasesForAdminQueues', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    ALTER TABLE [Vendor] ADD [PayoutDay] nvarchar(20) NOT NULL DEFAULT N'Monday';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    ALTER TABLE [Drivers] ADD [PayoutDay] nvarchar(20) NOT NULL DEFAULT N'Monday';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    UPDATE [Vendor]
    SET [FinancialLifecycleMode] = N'Weekly',
        [PayoutCycle] = N'weekly',
        [PayoutDay] = N'Monday'
    WHERE [FinancialLifecycleMode] = N'PerOrderDirectPayout'
       OR LOWER(LTRIM(RTRIM(COALESCE([PayoutCycle], N'')))) IN
          (N'per_order_direct_payout', N'perorderdirectpayout', N'per-order-direct-payout', N'order_by_order', N'orderbyorder');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    CREATE TABLE [PayoutManualConfirmations] (
        [Id] uniqueidentifier NOT NULL,
        [PayoutId] uniqueidentifier NOT NULL,
        [TransferReference] nvarchar(200) NOT NULL,
        [ProofUrl] nvarchar(2000) NOT NULL,
        [ConfirmedByUserId] uniqueidentifier NOT NULL,
        [ConfirmedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PayoutManualConfirmations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayoutManualConfirmations_Payouts_PayoutId] FOREIGN KEY ([PayoutId]) REFERENCES [Payouts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    CREATE TABLE [SettlementProcessingModeAudits] (
        [Id] uniqueidentifier NOT NULL,
        [PreviousMode] nvarchar(20) NOT NULL,
        [NewMode] nvarchar(20) NOT NULL,
        [ChangedByUserId] uniqueidentifier NOT NULL,
        [ChangedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SettlementProcessingModeAudits] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    CREATE TABLE [SettlementProcessingSettings] (
        [Id] uniqueidentifier NOT NULL,
        [Mode] nvarchar(20) NOT NULL DEFAULT N'Automatic',
        [UpdatedByUserId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SettlementProcessingSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    CREATE INDEX [IX_PayoutManualConfirmations_ConfirmedBy_ConfirmedAt] ON [PayoutManualConfirmations] ([ConfirmedByUserId], [ConfirmedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayoutManualConfirmations_PayoutId] ON [PayoutManualConfirmations] ([PayoutId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    CREATE INDEX [IX_SettlementProcessingModeAudits_ChangedAtUtc] ON [SettlementProcessingModeAudits] ([ChangedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721151605_AddManualSettlementProcessingAndPayoutScheduling', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721180654_AddConfigurablePayoutDays'
)
BEGIN
    ALTER TABLE [SettlementProcessingSettings] ADD [PayoutDays] nvarchar(200) NOT NULL DEFAULT N'Monday,Thursday';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721180654_AddConfigurablePayoutDays'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721180654_AddConfigurablePayoutDays', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    DROP INDEX [IX_DriverWithdrawalRequests_PayoutId] ON [DriverWithdrawalRequests];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    ALTER TABLE [SettlementProcessingSettings] ADD [RequireManualPayoutDualControl] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    ALTER TABLE [Payouts] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    CREATE TABLE [PayoutExecutionReservations] (
        [Id] uniqueidentifier NOT NULL,
        [PayoutId] uniqueidentifier NOT NULL,
        [Mode] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ClaimedByUserId] uniqueidentifier NULL,
        [ClaimedAtUtc] datetime2 NOT NULL,
        [SubmittedByUserId] uniqueidentifier NULL,
        [SubmittedAtUtc] datetime2 NULL,
        [SubmissionReference] nvarchar(200) NULL,
        [ReleasedByUserId] uniqueidentifier NULL,
        [ReleasedAtUtc] datetime2 NULL,
        [ReleaseReason] nvarchar(1000) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PayoutExecutionReservations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayoutExecutionReservations_Payouts_PayoutId] FOREIGN KEY ([PayoutId]) REFERENCES [Payouts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    CREATE TABLE [PayoutReversals] (
        [Id] uniqueidentifier NOT NULL,
        [PayoutId] uniqueidentifier NOT NULL,
        [ReturnReference] nvarchar(200) NOT NULL,
        [ProofUrl] nvarchar(2000) NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [ConfirmedByUserId] uniqueidentifier NOT NULL,
        [ConfirmedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PayoutReversals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayoutReversals_Payouts_PayoutId] FOREIGN KEY ([PayoutId]) REFERENCES [Payouts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_DriverWithdrawalRequests_PayoutId] ON [DriverWithdrawalRequests] ([PayoutId]) WHERE [PayoutId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    CREATE INDEX [IX_PayoutExecutionReservations_Mode_Status_ClaimedAt] ON [PayoutExecutionReservations] ([Mode], [Status], [ClaimedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayoutExecutionReservations_PayoutId] ON [PayoutExecutionReservations] ([PayoutId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    CREATE INDEX [IX_PayoutReversals_ConfirmedBy_ConfirmedAt] ON [PayoutReversals] ([ConfirmedByUserId], [ConfirmedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayoutReversals_PayoutId] ON [PayoutReversals] ([PayoutId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    INSERT INTO [PayoutExecutionReservations]
        ([Id], [PayoutId], [Mode], [Status], [ClaimedByUserId], [ClaimedAtUtc],
         [SubmittedByUserId], [SubmittedAtUtc], [SubmissionReference],
         [ReleasedByUserId], [ReleasedAtUtc], [ReleaseReason], [CreatedAtUtc], [UpdatedAtUtc])
    SELECT
        NEWID(),
        [p].[Id],
        N'Manual',
        N'Submitted',
        NULL,
        COALESCE([p].[TriggeredAtUtc], [p].[CreatedAtUtc]),
        NULL,
        COALESCE([p].[TriggeredAtUtc], [p].[CreatedAtUtc]),
        N'Legacy manual payout awaiting confirmation',
        NULL,
        NULL,
        NULL,
        [p].[CreatedAtUtc],
        [p].[UpdatedAtUtc]
    FROM [Payouts] AS [p]
    WHERE [p].[ProviderName] = N'Manual'
      AND [p].[Status] IN (N'Queued', N'Processing')
      AND [p].[ProviderTransferId] IS NULL
      AND NOT EXISTS (
          SELECT 1
          FROM [PayoutExecutionReservations] AS [r]
          WHERE [r].[PayoutId] = [p].[Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721185614_HardenPayoutExecutionSafety'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721185614_HardenPayoutExecutionSafety', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    ALTER TABLE [SettlementProcessingSettings] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    ALTER TABLE [Payouts] ADD [ScheduledPayoutDay] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    DECLARE @var40 sysname;
    SELECT @var40 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PayoutReversals]') AND [c].[name] = N'ProofUrl');
    IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [PayoutReversals] DROP CONSTRAINT [' + @var40 + '];');
    ALTER TABLE [PayoutReversals] ALTER COLUMN [ProofUrl] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    ALTER TABLE [PayoutReversals] ADD [ProofAttachmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    DECLARE @var41 sysname;
    SELECT @var41 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PayoutManualConfirmations]') AND [c].[name] = N'ProofUrl');
    IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [PayoutManualConfirmations] DROP CONSTRAINT [' + @var41 + '];');
    ALTER TABLE [PayoutManualConfirmations] ALTER COLUMN [ProofUrl] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    ALTER TABLE [PayoutManualConfirmations] ADD [ProofAttachmentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE TABLE [PayoutBankStatementImports] (
        [Id] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [FileSha256] nvarchar(64) NOT NULL,
        [ImportedByUserId] uniqueidentifier NOT NULL,
        [ImportedAtUtc] datetime2 NOT NULL,
        [TotalRows] int NOT NULL,
        [MatchedRows] int NOT NULL,
        [UnmatchedRows] int NOT NULL,
        [AmbiguousRows] int NOT NULL,
        [MismatchRows] int NOT NULL,
        [InvalidRows] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PayoutBankStatementImports] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE TABLE [PayoutProofAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [PayoutId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(32) NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [ContentLength] bigint NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [ProtectedContent] varbinary(max) NOT NULL,
        [UploadedByUserId] uniqueidentifier NOT NULL,
        [UploadedAtUtc] datetime2 NOT NULL,
        [FinalizedByUserId] uniqueidentifier NULL,
        [FinalizedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PayoutProofAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayoutProofAttachments_Payouts_PayoutId] FOREIGN KEY ([PayoutId]) REFERENCES [Payouts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE TABLE [PayoutBankStatementEntries] (
        [Id] uniqueidentifier NOT NULL,
        [ImportId] uniqueidentifier NOT NULL,
        [RowNumber] int NOT NULL,
        [BankReference] nvarchar(200) NOT NULL,
        [NormalizedBankReference] nvarchar(200) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [TransactionDateUtc] datetime2 NOT NULL,
        [CurrencyCode] nvarchar(8) NOT NULL,
        [BeneficiaryMasked] nvarchar(256) NULL,
        [Memo] nvarchar(500) NULL,
        [Status] nvarchar(20) NOT NULL,
        [PayoutId] uniqueidentifier NULL,
        [MatchedByUserId] uniqueidentifier NULL,
        [MatchedAtUtc] datetime2 NULL,
        [ResolutionNote] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PayoutBankStatementEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayoutBankStatementEntries_PayoutBankStatementImports_ImportId] FOREIGN KEY ([ImportId]) REFERENCES [PayoutBankStatementImports] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PayoutBankStatementEntries_Payouts_PayoutId] FOREIGN KEY ([PayoutId]) REFERENCES [Payouts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE INDEX [IX_PayoutReversals_ProofAttachmentId] ON [PayoutReversals] ([ProofAttachmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE INDEX [IX_PayoutManualConfirmations_ProofAttachmentId] ON [PayoutManualConfirmations] ([ProofAttachmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE INDEX [IX_PayoutBankStatementEntries_Reference_Amount] ON [PayoutBankStatementEntries] ([NormalizedBankReference], [Amount]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE INDEX [IX_PayoutBankStatementEntries_Status_Date] ON [PayoutBankStatementEntries] ([Status], [TransactionDateUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PayoutBankStatementEntries_Import_Row] ON [PayoutBankStatementEntries] ([ImportId], [RowNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_PayoutBankStatementEntries_PayoutId] ON [PayoutBankStatementEntries] ([PayoutId]) WHERE [PayoutId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE INDEX [IX_PayoutBankStatementImports_ImportedAt_ImportedBy] ON [PayoutBankStatementImports] ([ImportedAtUtc], [ImportedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PayoutBankStatementImports_FileSha256] ON [PayoutBankStatementImports] ([FileSha256]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE INDEX [IX_PayoutProofAttachments_PayoutId_Kind_FinalizedAt] ON [PayoutProofAttachments] ([PayoutId], [Kind], [FinalizedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PayoutProofAttachments_PayoutId_Kind_Sha256] ON [PayoutProofAttachments] ([PayoutId], [Kind], [Sha256]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    ALTER TABLE [PayoutManualConfirmations] ADD CONSTRAINT [FK_PayoutManualConfirmations_PayoutProofAttachments_ProofAttachmentId] FOREIGN KEY ([ProofAttachmentId]) REFERENCES [PayoutProofAttachments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    ALTER TABLE [PayoutReversals] ADD CONSTRAINT [FK_PayoutReversals_PayoutProofAttachments_ProofAttachmentId] FOREIGN KEY ([ProofAttachmentId]) REFERENCES [PayoutProofAttachments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260721195706_CompleteManualSettlementOperationalHardening'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260721195706_CompleteManualSettlementOperationalHardening', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    DROP INDEX [IX_DriverWithdrawalRequests_DriverId] ON [DriverWithdrawalRequests];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    ALTER TABLE [DriverWithdrawalRequests] ADD [DestinationSnapshot] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    ALTER TABLE [DriverWithdrawalRequests] ADD [RequestIdempotencyKey] nvarchar(160) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    ALTER TABLE [DriverWithdrawalRequests] ADD [RequestedPayoutDay] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    ALTER TABLE [DriverWithdrawalRequests] ADD [ReviewedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    ALTER TABLE [DriverWithdrawalRequests] ADD [ReviewedByUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    DECLARE @var42 sysname;
    SELECT @var42 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DriverPayoutMethods]') AND [c].[name] = N'AccountIdentifier');
    IF @var42 IS NOT NULL EXEC(N'ALTER TABLE [DriverPayoutMethods] DROP CONSTRAINT [' + @var42 + '];');
    ALTER TABLE [DriverPayoutMethods] ALTER COLUMN [AccountIdentifier] nvarchar(512) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    DECLARE @var43 sysname;
    SELECT @var43 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DriverPayoutMethods]') AND [c].[name] = N'AccountHolderName');
    IF @var43 IS NOT NULL EXEC(N'ALTER TABLE [DriverPayoutMethods] DROP CONSTRAINT [' + @var43 + '];');
    ALTER TABLE [DriverPayoutMethods] ALTER COLUMN [AccountHolderName] nvarchar(512) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    UPDATE withdrawal
    SET [RequestedPayoutDay] = driver.[PayoutDay]
    FROM [DriverWithdrawalRequests] AS withdrawal
    INNER JOIN [Drivers] AS driver ON driver.[Id] = withdrawal.[DriverId]
    WHERE withdrawal.[RequestedPayoutDay] IS NULL;

    DECLARE @DuplicateActiveWithdrawals TABLE ([Id] uniqueidentifier PRIMARY KEY);

    INSERT INTO @DuplicateActiveWithdrawals ([Id])
    SELECT ranked.[Id]
    FROM
    (
        SELECT
            withdrawal.[Id],
            ROW_NUMBER() OVER
            (
                PARTITION BY withdrawal.[DriverId]
                ORDER BY
                    CASE WHEN withdrawal.[Status] = 'Processing' THEN 0 ELSE 1 END,
                    withdrawal.[CreatedAtUtc],
                    withdrawal.[Id]
            ) AS [RowNumber]
        FROM [DriverWithdrawalRequests] AS withdrawal
        WHERE withdrawal.[Status] IN ('Pending', 'Processing')
    ) AS ranked
    WHERE ranked.[RowNumber] > 1;

    UPDATE hold
    SET
        hold.[Status] = 'Cancelled',
        hold.[CancelledAtUtc] = SYSUTCDATETIME(),
        hold.[FailureReason] = COALESCE(
            hold.[FailureReason],
            'Duplicate active withdrawal closed during payout safety migration.')
    FROM [WalletHolds] AS hold
    INNER JOIN @DuplicateActiveWithdrawals AS duplicate
        ON duplicate.[Id] = hold.[ReferenceId]
    WHERE hold.[ReferenceType] = 'DriverWithdrawalRequest'
      AND hold.[Status] = 'Active';

    UPDATE withdrawal
    SET
        withdrawal.[Status] = 'Cancelled',
        withdrawal.[FailureReason] = COALESCE(
            withdrawal.[FailureReason],
            'Duplicate active withdrawal closed during payout safety migration.'),
        withdrawal.[ProcessedAtUtc] = SYSUTCDATETIME()
    FROM [DriverWithdrawalRequests] AS withdrawal
    INNER JOIN @DuplicateActiveWithdrawals AS duplicate
        ON duplicate.[Id] = withdrawal.[Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    CREATE INDEX [IX_DriverWithdrawalRequests_Driver_Status] ON [DriverWithdrawalRequests] ([DriverId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_DriverWithdrawalRequests_Driver_IdempotencyKey] ON [DriverWithdrawalRequests] ([DriverId], [RequestIdempotencyKey]) WHERE [RequestIdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_DriverWithdrawalRequests_OneActivePerDriver] ON [DriverWithdrawalRequests] ([DriverId]) WHERE [Status] IN (''Pending'', ''Processing'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722103228_HardenDriverWithdrawalSettlementWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722103228_HardenDriverWithdrawalSettlementWorkflow', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722151444_ExpandEncryptedDestinationSnapshotCapacity'
)
BEGIN
    ALTER TABLE [Payouts] ALTER COLUMN [DestinationSnapshot] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722151444_ExpandEncryptedDestinationSnapshotCapacity'
)
BEGIN
    ALTER TABLE [DriverWithdrawalRequests] ALTER COLUMN [DestinationSnapshot] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722151444_ExpandEncryptedDestinationSnapshotCapacity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722151444_ExpandEncryptedDestinationSnapshotCapacity', N'9.0.3');
END;

COMMIT;
GO

