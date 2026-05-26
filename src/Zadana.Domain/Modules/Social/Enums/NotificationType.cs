namespace Zadana.Domain.Modules.Social.Enums;

public static class NotificationTypes
{
    public const string OrderStatusChanged = "order_status_changed";
    public const string NewBanner = "new_banner";
    public const string OrderPlaced = "order_placed";
    public const string OrderCancelled = "order_cancelled";
    public const string VendorNewOrder = "vendor_new_order";
    public const string VendorAccountUpdated = "vendor_account_updated";
    public const string VendorSettlementPaid = "vendor_settlement_paid";
    public const string OrderSupportCaseChanged = "order_support_case_changed";
    public const string OrderSupportCase = "order_support_case";
    public const string VendorSupportTicketChanged = "vendor_support_ticket_changed";
    public const string AdminOrderSupportCaseCreated = "admin_order_support_case_created";
    public const string AdminOrderSupportCaseAssigned = "admin_order_support_case_assigned";
    public const string AdminOrderSupportCaseEscalated = "admin_order_support_case_escalated";
    public const string DriverDeliveryOffer = "delivery-offer";
    public const string DriverAssignmentUpdated = "driver_assignment_updated";
    public const string DriverWalletUpdated = "driver_wallet_updated";
    public const string DriverAccountUpdated = "driver_account_updated";
    public const string DriverCommitmentEnforcement = "driver_commitment_enforcement";
}
