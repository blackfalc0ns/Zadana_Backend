using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

/// <summary>
/// Repairs production databases whose migration history survived while critical
/// tables created by older migrations were removed or never provisioned.
///
/// This migration intentionally runs before AddHotPathIndexesForLoad because
/// that migration replaces the legacy DeliveryAssignments indexes.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260601000000_RepairMissingCriticalTables")]
public sealed class RepairMissingCriticalTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
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
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately no-op. These are historical application tables, and a
        // rollback must never drop tables that may predate this repair.
    }
}
