-- Idempotent SQL for:
-- 20260726163201_DropPendingRegistrations
-- Pending signup state is now a signed JWT; no DB table is needed.
-- Run against the production database (sqlcmd / SSMS)
-- WITH or BEFORE deploying the API that removes PendingRegistrations usage.

BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'[PendingRegistrations]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [PendingRegistrations];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726163201_DropPendingRegistrations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726163201_DropPendingRegistrations', N'9.0.3');
END;
GO

COMMIT;
GO
