/*
  Wipe ALL orders + related rows (SQL Server).
  Requires: SET QUOTED_IDENTIFIER ON (filtered indexes / computed columns).
*/

SET XACT_ABORT ON;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;

BEGIN TRAN;

DECLARE @OrdersBefore INT = (SELECT COUNT(1) FROM dbo.Orders);
PRINT CONCAT('Orders before wipe: ', @OrdersBefore);

IF OBJECT_ID(N'dbo.RefundAllocations', N'U') IS NOT NULL
BEGIN
    DELETE ra
    FROM dbo.RefundAllocations ra
    INNER JOIN dbo.Refunds r ON r.Id = ra.RefundId
    INNER JOIN dbo.Payments p ON p.Id = r.PaymentId
    WHERE p.OrderId IN (SELECT Id FROM dbo.Orders);
END

IF OBJECT_ID(N'dbo.Refunds', N'U') IS NOT NULL
BEGIN
    DELETE r
    FROM dbo.Refunds r
    INNER JOIN dbo.Payments p ON p.Id = r.PaymentId
    WHERE p.OrderId IN (SELECT Id FROM dbo.Orders);
END

IF OBJECT_ID(N'dbo.PaymentGatewaySettlementItems', N'U') IS NOT NULL
    DELETE FROM dbo.PaymentGatewaySettlementItems WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.SettlementItems', N'U') IS NOT NULL
    DELETE FROM dbo.SettlementItems WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.WalletTransactions', N'U') IS NOT NULL
    DELETE FROM dbo.WalletTransactions WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.JournalLines', N'U') IS NOT NULL
    DELETE FROM dbo.JournalLines WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.FinancialEvents', N'U') IS NOT NULL
    DELETE FROM dbo.FinancialEvents WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.Payments', N'U') IS NOT NULL
    DELETE FROM dbo.Payments WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.DeliveryProofs', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.DeliveryProofs', N'AssignmentId') IS NOT NULL
BEGIN
    DELETE dp
    FROM dbo.DeliveryProofs dp
    INNER JOIN dbo.DeliveryAssignments da ON da.Id = dp.AssignmentId
    WHERE da.OrderId IN (SELECT Id FROM dbo.Orders);
END

IF OBJECT_ID(N'dbo.DeliveryOfferAttempts', N'U') IS NOT NULL
    DELETE FROM dbo.DeliveryOfferAttempts WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.DeliveryAssignments', N'U') IS NOT NULL
    DELETE FROM dbo.DeliveryAssignments WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.VendorSupportTickets', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.VendorSupportTickets', N'OrderId') IS NOT NULL
    UPDATE dbo.VendorSupportTickets SET OrderId = NULL WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.Notifications', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.Notifications', N'ReferenceId') IS NOT NULL
    DELETE FROM dbo.Notifications WHERE ReferenceId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.AdminAlertEvents', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.AdminAlertEvents', N'ReferenceId') IS NOT NULL
    DELETE FROM dbo.AdminAlertEvents WHERE ReferenceId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.OrderSupportCaseAttachments', N'U') IS NOT NULL
BEGIN
    DELETE a
    FROM dbo.OrderSupportCaseAttachments a
    INNER JOIN dbo.OrderSupportCases c ON c.Id = a.OrderSupportCaseId
    WHERE c.OrderId IN (SELECT Id FROM dbo.Orders);
END

IF OBJECT_ID(N'dbo.OrderSupportCaseActivities', N'U') IS NOT NULL
BEGIN
    DELETE a
    FROM dbo.OrderSupportCaseActivities a
    INNER JOIN dbo.OrderSupportCases c ON c.Id = a.OrderSupportCaseId
    WHERE c.OrderId IN (SELECT Id FROM dbo.Orders);
END

IF OBJECT_ID(N'dbo.OrderSupportCases', N'U') IS NOT NULL
    DELETE FROM dbo.OrderSupportCases WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.OrderComplaintAttachments', N'U') IS NOT NULL
BEGIN
    DELETE a
    FROM dbo.OrderComplaintAttachments a
    INNER JOIN dbo.OrderComplaints c ON c.Id = a.OrderComplaintId
    WHERE c.OrderId IN (SELECT Id FROM dbo.Orders);
END

IF OBJECT_ID(N'dbo.OrderComplaints', N'U') IS NOT NULL
    DELETE FROM dbo.OrderComplaints WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.OrderCancellationRequests', N'U') IS NOT NULL
    DELETE FROM dbo.OrderCancellationRequests WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.OrderStatusHistories', N'U') IS NOT NULL
    DELETE FROM dbo.OrderStatusHistories WHERE OrderId IN (SELECT Id FROM dbo.Orders);

IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL
    DELETE FROM dbo.OrderItems WHERE OrderId IN (SELECT Id FROM dbo.Orders);

DELETE FROM dbo.Orders;

DECLARE @OrdersAfter INT = (SELECT COUNT(1) FROM dbo.Orders);
PRINT CONCAT('Orders after wipe: ', @OrdersAfter);

IF @OrdersAfter <> 0
BEGIN
    ROLLBACK;
    THROW 50001, N'Order wipe failed — rolled back.', 1;
END

COMMIT;
PRINT N'All orders wiped successfully.';
