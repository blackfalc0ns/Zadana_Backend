-- Idempotent SQL for:
-- 20260726140756_AddPendingRegistration
-- Run against the production database (sqlcmd / SSMS)
-- BEFORE or WITH the new API deploy.

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726140756_AddPendingRegistration'
)
BEGIN
    CREATE TABLE [PendingRegistrations] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [PhoneNumber] nvarchar(32) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [ProfilePhotoUrl] nvarchar(1000) NULL,
        [OtpCodeHash] nvarchar(128) NULL,
        [OtpExpiryUtc] datetime2 NULL,
        [OtpAttempts] int NOT NULL,
        [LastOtpSentAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PendingRegistrations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726140756_AddPendingRegistration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PendingRegistrations_Email] ON [PendingRegistrations] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726140756_AddPendingRegistration'
)
BEGIN
    CREATE INDEX [IX_PendingRegistrations_ExpiresAtUtc] ON [PendingRegistrations] ([ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726140756_AddPendingRegistration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PendingRegistrations_PhoneNumber] ON [PendingRegistrations] ([PhoneNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726140756_AddPendingRegistration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726140756_AddPendingRegistration', N'9.0.3');
END;

COMMIT;
GO

