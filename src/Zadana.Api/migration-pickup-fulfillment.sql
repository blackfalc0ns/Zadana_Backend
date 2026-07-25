-- Idempotent SQL for:
-- 20260725144940_AddPickupFulfillment
-- 20260725152835_AddPickupCashOnPickup
-- Run against the production database (SSMS / Azure Data Studio / sqlcmd)
-- BEFORE or WITH the new API deploy. App seed will also insert PlatformPickupSettings if missing.

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'CustomerAddressId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [Orders] ALTER COLUMN [CustomerAddressId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [ConvertedToDeliveryAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [DeliveryUpgradePaymentId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [Fulfillment] nvarchar(20) NOT NULL DEFAULT N'Delivery';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupNoShowDeadlineUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupOtpCode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupOtpExpiresAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupOtpFailedAttempts] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupOtpLockedUntilUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupOtpResendCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupOtpResendWindowStartedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupOtpVerifiedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupOtpVerifiedByVendorUserId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupReminder50Sent] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [PickupReminder90Sent] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [ReadyForPickupAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    ALTER TABLE [Orders] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    CREATE TABLE [OrderCancellationRequests] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [RequestedByUserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CustomerReason] nvarchar(1000) NULL,
        [VendorResponseNote] nvarchar(1000) NULL,
        [DecidedByUserId] uniqueidentifier NULL,
        [DecidedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderCancellationRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderCancellationRequests_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    CREATE TABLE [PlatformPickupSettings] (
        [Id] uniqueidentifier NOT NULL,
        [DeliveryOptionEnabled] bit NOT NULL,
        [PickupOptionEnabled] bit NOT NULL,
        [PickupCommissionPercent] decimal(5,2) NOT NULL,
        [PickupNoShowTimeoutHours] int NOT NULL,
        [PickupOtpMaxAttempts] int NOT NULL,
        [PickupOtpLockoutMinutes] int NOT NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PlatformPickupSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    CREATE INDEX [IX_Orders_Fulfillment_Status_NoShowDeadline] ON [Orders] ([Fulfillment], [Status], [PickupNoShowDeadlineUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    CREATE INDEX [IX_Orders_Fulfillment_Status_ReadyForPickup] ON [Orders] ([Fulfillment], [Status], [ReadyForPickupAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    CREATE INDEX [IX_OrderCancellationRequests_OrderId_Status] ON [OrderCancellationRequests] ([OrderId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725144940_AddPickupFulfillment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260725144940_AddPickupFulfillment', N'9.0.3');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725152835_AddPickupCashOnPickup'
)
BEGIN
    ALTER TABLE [PlatformPickupSettings] ADD [PickupCashOnPickupEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260725152835_AddPickupCashOnPickup'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260725152835_AddPickupCashOnPickup', N'9.0.3');
END;

-- Seed singleton pickup settings (matches ApplicationDbContextInitialiser)
IF OBJECT_ID(N'[PlatformPickupSettings]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM [PlatformPickupSettings]
        WHERE [Id] = '00000000-0000-0000-0000-0000000000d1'
   )
BEGIN
    INSERT INTO [PlatformPickupSettings] (
        [Id],
        [DeliveryOptionEnabled],
        [PickupOptionEnabled],
        [PickupCashOnPickupEnabled],
        [PickupCommissionPercent],
        [PickupNoShowTimeoutHours],
        [PickupOtpMaxAttempts],
        [PickupOtpLockoutMinutes],
        [UpdatedByUserId],
        [CreatedAtUtc],
        [UpdatedAtUtc]
    )
    VALUES (
        '00000000-0000-0000-0000-0000000000d1',
        CAST(1 AS bit),
        CAST(1 AS bit),
        CAST(0 AS bit),
        CAST(5.00 AS decimal(5,2)),
        24,
        5,
        30,
        NULL,
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END;

COMMIT;
GO

