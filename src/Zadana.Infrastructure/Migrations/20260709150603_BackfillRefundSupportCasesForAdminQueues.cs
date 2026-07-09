using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRefundSupportCasesForAdminQueues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @BackfilledCases TABLE
                (
                    CaseId uniqueidentifier NOT NULL PRIMARY KEY
                );

                INSERT INTO @BackfilledCases (CaseId)
                SELECT Id
                FROM [OrderSupportCases]
                WHERE ReasonCode = N'admin_refund_backfill'
                  AND InitiatorRole = N'admin';

                UPDATE r
                SET r.OrderSupportCaseId = NULL
                FROM [Refunds] AS r
                INNER JOIN @BackfilledCases AS cases ON cases.CaseId = r.OrderSupportCaseId;

                DELETE activity
                FROM [OrderSupportCaseActivities] AS activity
                INNER JOIN @BackfilledCases AS cases ON cases.CaseId = activity.OrderSupportCaseId;

                DELETE supportCase
                FROM [OrderSupportCases] AS supportCase
                INNER JOIN @BackfilledCases AS cases ON cases.CaseId = supportCase.Id;
                """);
        }
    }
}
