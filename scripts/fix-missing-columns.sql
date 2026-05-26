-- Run this against the production/staging database to add missing OTP columns.
-- These columns exist in the EF model but were not created by the migration
-- (the migration was recorded in __EFMigrationsHistory without executing).

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'OtpAttempts')
    ALTER TABLE [AspNetUsers] ADD [OtpAttempts] int NOT NULL CONSTRAINT DF_AspNetUsers_OtpAttempts DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'OtpLockoutCount')
    ALTER TABLE [AspNetUsers] ADD [OtpLockoutCount] int NOT NULL CONSTRAINT DF_AspNetUsers_OtpLockoutCount DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'OtpLockedUntilUtc')
    ALTER TABLE [AspNetUsers] ADD [OtpLockedUntilUtc] datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'PasswordResetOtpAttempts')
    ALTER TABLE [AspNetUsers] ADD [PasswordResetOtpAttempts] int NOT NULL CONSTRAINT DF_AspNetUsers_PasswordResetOtpAttempts DEFAULT 0;

-- Also add the DriverLatestLocations table and NationalIdHash if missing
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DriverLatestLocations')
BEGIN
    CREATE TABLE [DriverLatestLocations] (
        [DriverId] uniqueidentifier NOT NULL,
        [Latitude] decimal(10,7) NOT NULL,
        [Longitude] decimal(10,7) NOT NULL,
        [AccuracyMeters] decimal(8,2) NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_DriverLatestLocations] PRIMARY KEY ([DriverId]),
        CONSTRAINT [FK_DriverLatestLocations_Drivers_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Drivers]([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Drivers') AND name = 'NationalIdHash')
    ALTER TABLE [Drivers] ADD [NationalIdHash] nvarchar(64) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Drivers_NationalIdHash')
    CREATE INDEX [IX_Drivers_NationalIdHash] ON [Drivers]([NationalIdHash]) WHERE [NationalIdHash] IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DriverLocations_DriverId_RecordedAt_Desc')
    CREATE INDEX [IX_DriverLocations_DriverId_RecordedAt_Desc] ON [DriverLocations]([DriverId] ASC, [RecordedAtUtc] DESC);
