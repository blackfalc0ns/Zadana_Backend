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
CREATE TABLE [Brand] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [LogoUrl] nvarchar(500) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Brand] PRIMARY KEY ([Id])
);

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

CREATE TABLE [ImageBank] (
    [Id] uniqueidentifier NOT NULL,
    [Url] nvarchar(1000) NOT NULL,
    [AltText] nvarchar(200) NULL,
    [Tags] nvarchar(500) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ImageBank] PRIMARY KEY ([Id])
);

CREATE TABLE [UnitOfMeasure] (
    [Id] uniqueidentifier NOT NULL,
    [NameAr] nvarchar(100) NOT NULL,
    [NameEn] nvarchar(100) NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_UnitOfMeasure] PRIMARY KEY ([Id])
);

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

CREATE TABLE [MasterProduct] (
    [Id] uniqueidentifier NOT NULL,
    [NameAr] nvarchar(300) NOT NULL,
    [NameEn] nvarchar(300) NOT NULL,
    [Slug] nvarchar(300) NOT NULL,
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

CREATE TABLE [MasterProductImage] (
    [MasterProductId] uniqueidentifier NOT NULL,
    [ImageBankId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL DEFAULT 0,
    [IsPrimary] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_MasterProductImage] PRIMARY KEY ([MasterProductId], [ImageBankId]),
    CONSTRAINT [FK_MasterProductImage_ImageBank_ImageBankId] FOREIGN KEY ([ImageBankId]) REFERENCES [ImageBank] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MasterProductImage_MasterProduct_MasterProductId] FOREIGN KEY ([MasterProductId]) REFERENCES [MasterProduct] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [DriverLocations] (
    [Id] uniqueidentifier NOT NULL,
    [DriverId] uniqueidentifier NOT NULL,
    [Latitude] decimal(10,7) NOT NULL,
    [Longitude] decimal(10,7) NOT NULL,
    [RecordedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_DriverLocations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DriverLocations_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers] ([Id]) ON DELETE CASCADE
);

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

CREATE TABLE [CouponVendors] (
    [Id] uniqueidentifier NOT NULL,
    [CouponId] uniqueidentifier NOT NULL,
    [VendorId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CouponVendors] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CouponVendors_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CouponVendors_Vendor_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendor] ([Id]) ON DELETE CASCADE
);

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

CREATE UNIQUE INDEX [IX_BranchOpHour_Branch_Day] ON [BranchOperatingHour] ([BranchId], [DayOfWeek]);

CREATE UNIQUE INDEX [IX_CartItems_CartId_VendorProductId] ON [CartItems] ([CartId], [VendorProductId]);

CREATE INDEX [IX_CartItems_VendorProductId] ON [CartItems] ([VendorProductId]);

CREATE UNIQUE INDEX [IX_Carts_UserId_VendorId] ON [Carts] ([UserId], [VendorId]);

CREATE INDEX [IX_Carts_VendorId] ON [Carts] ([VendorId]);

CREATE INDEX [IX_Category_ParentId] ON [Category] ([ParentCategoryId]);

CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);

CREATE UNIQUE INDEX [IX_CouponVendors_CouponId_VendorId] ON [CouponVendors] ([CouponId], [VendorId]);

CREATE INDEX [IX_CouponVendors_VendorId] ON [CouponVendors] ([VendorId]);

CREATE INDEX [IX_CustomerAddresses_UserId] ON [CustomerAddresses] ([UserId]);

CREATE INDEX [IX_DeliveryAssignments_DriverId] ON [DeliveryAssignments] ([DriverId]);

CREATE INDEX [IX_DeliveryAssignments_OrderId] ON [DeliveryAssignments] ([OrderId]);

CREATE INDEX [IX_DeliveryProofs_AssignmentId] ON [DeliveryProofs] ([AssignmentId]);

CREATE INDEX [IX_DriverLocations_DriverId] ON [DriverLocations] ([DriverId]);

CREATE INDEX [IX_Drivers_UserId] ON [Drivers] ([UserId]);

CREATE UNIQUE INDEX [IX_MasterProduct_Barcode] ON [MasterProduct] ([Barcode]) WHERE [Barcode] IS NOT NULL;

CREATE INDEX [IX_MasterProduct_BrandId] ON [MasterProduct] ([BrandId]);

CREATE INDEX [IX_MasterProduct_CategoryId] ON [MasterProduct] ([CategoryId]);

CREATE INDEX [IX_MasterProduct_UnitOfMeasureId] ON [MasterProduct] ([UnitOfMeasureId]);

CREATE UNIQUE INDEX [IX_MasterProduct_Slug] ON [MasterProduct] ([Slug]);

CREATE INDEX [IX_MasterProductImage_ImageBankId] ON [MasterProductImage] ([ImageBankId]);

CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);

CREATE INDEX [IX_OrderItems_MasterProductId] ON [OrderItems] ([MasterProductId]);

CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);

CREATE INDEX [IX_OrderItems_VendorProductId] ON [OrderItems] ([VendorProductId]);

CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [Orders] ([OrderNumber]);

CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);

CREATE INDEX [IX_Orders_VendorBranchId] ON [Orders] ([VendorBranchId]);

CREATE INDEX [IX_Orders_VendorId] ON [Orders] ([VendorId]);

CREATE INDEX [IX_OrderStatusHistories_ChangedByUserId] ON [OrderStatusHistories] ([ChangedByUserId]);

CREATE INDEX [IX_OrderStatusHistories_OrderId] ON [OrderStatusHistories] ([OrderId]);

CREATE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);

CREATE INDEX [IX_Payouts_SettlementId] ON [Payouts] ([SettlementId]);

CREATE INDEX [IX_Payouts_VendorBankAccountId] ON [Payouts] ([VendorBankAccountId]);

CREATE INDEX [IX_RefreshToken_Token] ON [RefreshToken] ([Token]);

CREATE INDEX [IX_RefreshToken_UserId] ON [RefreshToken] ([UserId]);

CREATE INDEX [IX_Refunds_PaymentId] ON [Refunds] ([PaymentId]);

CREATE INDEX [IX_Reviews_OrderId] ON [Reviews] ([OrderId]);

CREATE INDEX [IX_Reviews_UserId] ON [Reviews] ([UserId]);

CREATE INDEX [IX_Reviews_VendorId] ON [Reviews] ([VendorId]);

CREATE INDEX [IX_SettlementItems_OrderId] ON [SettlementItems] ([OrderId]);

CREATE INDEX [IX_SettlementItems_SettlementId] ON [SettlementItems] ([SettlementId]);

CREATE INDEX [IX_Settlements_DriverId] ON [Settlements] ([DriverId]);

CREATE INDEX [IX_Settlements_VendorId] ON [Settlements] ([VendorId]);

CREATE UNIQUE INDEX [IX_User_Email] ON [User] ([Email]);

CREATE UNIQUE INDEX [IX_User_Phone] ON [User] ([Phone]);

CREATE UNIQUE INDEX [IX_Vendor_CommRegNum] ON [Vendor] ([CommercialRegistrationNumber]);

CREATE INDEX [IX_Vendor_Status] ON [Vendor] ([Status]);

CREATE UNIQUE INDEX [IX_Vendor_UserId] ON [Vendor] ([UserId]);

CREATE INDEX [IX_VendorBankAccount_VendorId] ON [VendorBankAccount] ([VendorId]);

CREATE INDEX [IX_VendorBranch_VendorId] ON [VendorBranch] ([VendorId]);

CREATE INDEX [IX_VendorProduct_MasterProductId] ON [VendorProduct] ([MasterProductId]);

CREATE UNIQUE INDEX [IX_VendorProduct_Vendor_Master] ON [VendorProduct] ([VendorId], [MasterProductId]);

CREATE INDEX [IX_VendorProduct_VendorBranchId] ON [VendorProduct] ([VendorBranchId]);

CREATE INDEX [IX_VendorProduct_VendorId] ON [VendorProduct] ([VendorId]);

CREATE UNIQUE INDEX [IX_Wallet_Owner] ON [Wallet] ([OwnerType], [OwnerId]);

CREATE INDEX [IX_WalletTransactions_OrderId] ON [WalletTransactions] ([OrderId]);

CREATE INDEX [IX_WalletTransactions_PaymentId] ON [WalletTransactions] ([PaymentId]);

CREATE INDEX [IX_WalletTransactions_SettlementId] ON [WalletTransactions] ([SettlementId]);

CREATE INDEX [IX_WalletTransactions_WalletId] ON [WalletTransactions] ([WalletId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260301144941_20260301_1650_GlobalInitialCreate', N'9.0.3');

DROP INDEX [IX_RefreshToken_Token] ON [RefreshToken];

CREATE UNIQUE INDEX [IX_RefreshToken_Token] ON [RefreshToken] ([Token]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260302152350_InitialIdentityMigration', N'9.0.3');

ALTER TABLE [Vendor] ADD [CommercialRegisterDocumentUrl] nvarchar(max) NULL;

ALTER TABLE [Vendor] ADD [LogoUrl] nvarchar(max) NULL;

ALTER TABLE [User] ADD [Address] nvarchar(max) NULL;

ALTER TABLE [User] ADD [Latitude] decimal(18,2) NULL;

ALTER TABLE [User] ADD [Longitude] decimal(18,2) NULL;

ALTER TABLE [User] ADD [ProfilePhotoUrl] nvarchar(max) NULL;

ALTER TABLE [Drivers] ADD [Address] nvarchar(max) NULL;

ALTER TABLE [Drivers] ADD [LicenseImageUrl] nvarchar(max) NULL;

ALTER TABLE [Drivers] ADD [NationalIdImageUrl] nvarchar(max) NULL;

ALTER TABLE [Drivers] ADD [PersonalPhotoUrl] nvarchar(max) NULL;

ALTER TABLE [Drivers] ADD [VehicleImageUrl] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260302165921_RegistrationFlows', N'9.0.3');

DECLARE @var sysname;
SELECT @var = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerAddresses]') AND [c].[name] = N'Label');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [CustomerAddresses] DROP CONSTRAINT [' + @var + '];');
ALTER TABLE [CustomerAddresses] ALTER COLUMN [Label] nvarchar(50) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260304035546_AddressLabelEnum', N'9.0.3');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260304040120_MovedCustomerAddressToIdentity', N'9.0.3');

ALTER TABLE [User] ADD [OtpCode] nvarchar(max) NULL;

ALTER TABLE [User] ADD [OtpExpiryTime] datetime2 NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260304042550_AddUserOtpFields', N'9.0.3');

ALTER TABLE [User] ADD [PasswordResetOtp] nvarchar(max) NULL;

ALTER TABLE [User] ADD [PasswordResetOtpExpiry] datetime2 NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260304150515_RemoveRedundantUserAddressFields', N'9.0.3');

ALTER TABLE [VendorProduct] ADD [CustomDescriptionAr] nvarchar(1000) NULL;

ALTER TABLE [VendorProduct] ADD [CustomDescriptionEn] nvarchar(1000) NULL;

ALTER TABLE [VendorProduct] ADD [CustomNameAr] nvarchar(200) NULL;

ALTER TABLE [VendorProduct] ADD [CustomNameEn] nvarchar(200) NULL;

ALTER TABLE [ImageBank] ADD [RejectionReason] nvarchar(500) NULL;

ALTER TABLE [ImageBank] ADD [Status] nvarchar(20) NOT NULL DEFAULT N'';

ALTER TABLE [ImageBank] ADD [UploadedByVendorId] uniqueidentifier NULL;

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

CREATE INDEX [IX_ImageBank_Status] ON [ImageBank] ([Status]);

CREATE INDEX [IX_ImageBank_UploadedByVendorId] ON [ImageBank] ([UploadedByVendorId]);

CREATE INDEX [IX_ProductRequest_Status] ON [ProductRequest] ([Status]);

CREATE INDEX [IX_ProductRequest_SuggestedCategoryId] ON [ProductRequest] ([SuggestedCategoryId]);

CREATE INDEX [IX_ProductRequest_VendorId] ON [ProductRequest] ([VendorId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260305131832_AddCatalogProductRequestsAndVendorOverrides', N'9.0.3');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260305131954_AddCatalogCategoryProductRequests_V2', N'9.0.3');

EXEC sp_rename N'[Brand].[Name]', N'NameEn', 'COLUMN';

ALTER TABLE [UnitOfMeasure] ADD [Symbol] nvarchar(20) NULL;

ALTER TABLE [Brand] ADD [NameAr] nvarchar(200) NOT NULL DEFAULT N'';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260305134014_AddBrandAndUnitBilingualSupport', N'9.0.3');

ALTER TABLE [Carts] DROP CONSTRAINT [FK_Carts_User_UserId];

ALTER TABLE [CustomerAddresses] DROP CONSTRAINT [FK_CustomerAddresses_User_UserId];

ALTER TABLE [Drivers] DROP CONSTRAINT [FK_Drivers_User_UserId];

ALTER TABLE [Notifications] DROP CONSTRAINT [FK_Notifications_User_UserId];

ALTER TABLE [Orders] DROP CONSTRAINT [FK_Orders_User_UserId];

ALTER TABLE [OrderStatusHistories] DROP CONSTRAINT [FK_OrderStatusHistories_User_ChangedByUserId];

ALTER TABLE [RefreshToken] DROP CONSTRAINT [FK_RefreshToken_User_UserId];

ALTER TABLE [Reviews] DROP CONSTRAINT [FK_Reviews_User_UserId];

ALTER TABLE [Vendor] DROP CONSTRAINT [FK_Vendor_User_UserId];

ALTER TABLE [User] DROP CONSTRAINT [PK_User];

DROP INDEX [IX_User_Email] ON [User];

DROP INDEX [IX_User_Phone] ON [User];

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[User]') AND [c].[name] = N'IsEmailVerified');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [User] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [User] DROP COLUMN [IsEmailVerified];

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[User]') AND [c].[name] = N'IsPhoneVerified');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [User] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [User] DROP COLUMN [IsPhoneVerified];

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[User]') AND [c].[name] = N'Phone');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [User] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [User] DROP COLUMN [Phone];

EXEC sp_rename N'[User]', N'AspNetUsers', 'OBJECT';

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'PasswordHash');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [PasswordHash] nvarchar(max) NULL;

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Email');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [Email] nvarchar(256) NULL;

ALTER TABLE [AspNetUsers] ADD [AccessFailedCount] int NOT NULL DEFAULT 0;

ALTER TABLE [AspNetUsers] ADD [ConcurrencyStamp] nvarchar(max) NULL;

ALTER TABLE [AspNetUsers] ADD [EmailConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AspNetUsers] ADD [LockoutEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AspNetUsers] ADD [LockoutEnd] datetimeoffset NULL;

ALTER TABLE [AspNetUsers] ADD [NormalizedEmail] nvarchar(256) NULL;

ALTER TABLE [AspNetUsers] ADD [NormalizedUserName] nvarchar(256) NULL;

ALTER TABLE [AspNetUsers] ADD [PhoneNumber] nvarchar(max) NULL;

ALTER TABLE [AspNetUsers] ADD [PhoneNumberConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AspNetUsers] ADD [SecurityStamp] nvarchar(max) NULL;

ALTER TABLE [AspNetUsers] ADD [TwoFactorEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [AspNetUsers] ADD [UserName] nvarchar(256) NULL;

ALTER TABLE [AspNetUsers] ADD CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]);

CREATE TABLE [AspNetRoles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] uniqueidentifier NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] uniqueidentifier NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

ALTER TABLE [Carts] ADD CONSTRAINT [FK_Carts_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [CustomerAddresses] ADD CONSTRAINT [FK_CustomerAddresses_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [Drivers] ADD CONSTRAINT [FK_Drivers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [OrderStatusHistories] ADD CONSTRAINT [FK_OrderStatusHistories_AspNetUsers_ChangedByUserId] FOREIGN KEY ([ChangedByUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL;

ALTER TABLE [RefreshToken] ADD CONSTRAINT [FK_RefreshToken_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

ALTER TABLE [Reviews] ADD CONSTRAINT [FK_Reviews_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Vendor] ADD CONSTRAINT [FK_Vendor_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260307102657_InitialIdentitySchema', N'9.0.3');

ALTER TABLE [Category] ADD [ImageUrl] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260307135750_AddCategoryImageUrl', N'9.0.3');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260307145408_ConsolidatePendingSets', N'9.0.3');

ALTER TABLE [MasterProductImage] DROP CONSTRAINT [FK_MasterProductImage_ImageBank_ImageBankId];

DROP TABLE [ImageBank];

ALTER TABLE [MasterProductImage] DROP CONSTRAINT [PK_MasterProductImage];

DROP INDEX [IX_MasterProductImage_ImageBankId] ON [MasterProductImage];

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MasterProductImage]') AND [c].[name] = N'ImageBankId');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [MasterProductImage] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [MasterProductImage] DROP COLUMN [ImageBankId];

ALTER TABLE [MasterProductImage] ADD [Url] nvarchar(500) NOT NULL DEFAULT N'';

ALTER TABLE [MasterProductImage] ADD [AltText] nvarchar(500) NULL;

ALTER TABLE [MasterProductImage] ADD CONSTRAINT [PK_MasterProductImage] PRIMARY KEY ([MasterProductId], [Url]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260308092338_UnifyProductAssets', N'9.0.3');

ALTER TABLE [AspNetUsers] ADD [LastOtpSentAt] datetime2 NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260309142633_AddLastOtpSentAt', N'9.0.3');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260312132141_AddSlugColumnToMasterProduct', N'9.0.3');

COMMIT;
GO

