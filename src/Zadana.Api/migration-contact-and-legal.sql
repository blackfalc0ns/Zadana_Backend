-- Idempotent SQL for:
-- 20260724151300_AddPlatformContactSettings
-- 20260724152709_AddPlatformLegalDocuments
-- Run against the production database (SSMS / Azure Data Studio / sqlcmd).

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
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724151300_AddPlatformContactSettings'
)
BEGIN
    CREATE TABLE [PlatformContactSettings] (
        [Id] uniqueidentifier NOT NULL,
        [SupportEmail] nvarchar(256) NULL,
        [SupportPhone] nvarchar(32) NULL,
        [WhatsAppUrl] nvarchar(500) NULL,
        [InstagramUrl] nvarchar(500) NULL,
        [TwitterUrl] nvarchar(500) NULL,
        [TikTokUrl] nvarchar(500) NULL,
        [SnapchatUrl] nvarchar(500) NULL,
        [FacebookUrl] nvarchar(500) NULL,
        [YouTubeUrl] nvarchar(500) NULL,
        [LinkedInUrl] nvarchar(500) NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PlatformContactSettings] PRIMARY KEY ([Id])
    );

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724151300_AddPlatformContactSettings', N'9.0.3');
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724152709_AddPlatformLegalDocuments'
)
BEGIN
    CREATE TABLE [PlatformLegalDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(50) NOT NULL,
        [ContentAr] nvarchar(max) NOT NULL,
        [ContentEn] nvarchar(max) NOT NULL,
        [Version] nvarchar(32) NOT NULL,
        [EffectiveAtUtc] datetime2 NOT NULL,
        [UpdatedByUserId] uniqueidentifier NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PlatformLegalDocuments] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [IX_PlatformLegalDocuments_DocumentType]
        ON [PlatformLegalDocuments] ([DocumentType]);

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724152709_AddPlatformLegalDocuments', N'9.0.3');
END;
GO

COMMIT;
GO
