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
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately no-op. These are historical application tables, and a
        // rollback must never drop tables that may predate this repair.
    }
}
