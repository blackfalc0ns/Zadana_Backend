namespace Zadana.Domain.Modules.Identity.Constants;

public static class PermissionKeys
{
    public static class Admin
    {
        public const string AccountView = "admin_account.view";
        public const string AccountEdit = "admin_account.edit";
        public const string DashboardView = "dashboard.view";
        public const string DashboardExport = "dashboard.export";
        public const string VendorsView = "vendors.view";
        public const string VendorsEdit = "vendors.edit";
        public const string VendorsApprove = "vendors.approve";
        public const string CatalogView = "catalog.view";
        public const string CatalogCreate = "catalog.create";
        public const string CatalogEdit = "catalog.edit";
        public const string CatalogApprove = "catalog.approve";
        public const string OrdersView = "orders.view";
        public const string OrdersEdit = "orders.edit";
        public const string OrdersApprove = "orders.approve";
        public const string CustomersView = "customers.view";
        public const string CustomersEdit = "customers.edit";
        public const string DriversView = "drivers.view";
        public const string DriversEdit = "drivers.edit";
        public const string DriversApprove = "drivers.approve";
        public const string DisputesView = "disputes.view";
        public const string DisputesEdit = "disputes.edit";
        public const string DisputesApprove = "disputes.approve";
        public const string FinancesView = "finances.view";
        public const string FinancesEdit = "finances.edit";
        public const string FinancesApprove = "finances.approve";
        public const string WalletsView = "wallets.view";
        public const string WalletsEdit = "wallets.edit";
        public const string WalletsApprove = "wallets.approve";
        public const string UsersAccessView = "users_access.view";
        public const string UsersAccessCreate = "users_access.create";
        public const string UsersAccessEdit = "users_access.edit";
        public const string UsersAccessApprove = "users_access.approve";
        public const string EmailCenterView = "email_center.view";
        public const string EmailCenterEdit = "email_center.edit";
        public const string MarketingView = "marketing.view";
        public const string MarketingEdit = "marketing.edit";
        public const string NotificationsView = "admin_notifications.view";
        public const string NotificationsEdit = "admin_notifications.edit";
        public const string DeliverySettingsView = "delivery_settings.view";
        public const string DeliverySettingsEdit = "delivery_settings.edit";
        public const string SystemManageSettings = "system.manage_settings";

        public static readonly string[] All =
        [
            AccountView, AccountEdit, DashboardView, DashboardExport,
            VendorsView, VendorsEdit, VendorsApprove,
            CatalogView, CatalogCreate, CatalogEdit, CatalogApprove,
            OrdersView, OrdersEdit, OrdersApprove,
            CustomersView, CustomersEdit,
            DriversView, DriversEdit, DriversApprove,
            DisputesView, DisputesEdit, DisputesApprove,
            FinancesView, FinancesEdit, FinancesApprove,
            WalletsView, WalletsEdit, WalletsApprove,
            UsersAccessView, UsersAccessCreate, UsersAccessEdit, UsersAccessApprove,
            EmailCenterView, EmailCenterEdit,
            MarketingView, MarketingEdit,
            NotificationsView, NotificationsEdit,
            DeliverySettingsView, DeliverySettingsEdit,
            SystemManageSettings
        ];

        public static readonly string[] Operations =
        [
            AccountView, AccountEdit, DashboardView, DashboardExport,
            VendorsView, VendorsEdit, VendorsApprove,
            CatalogView, CatalogCreate, CatalogEdit, CatalogApprove,
            OrdersView, OrdersEdit, OrdersApprove,
            CustomersView, CustomersEdit,
            DriversView, DriversEdit, DriversApprove,
            DisputesView, DisputesEdit, DisputesApprove,
            FinancesView,
            WalletsView, WalletsEdit,
            EmailCenterView, EmailCenterEdit,
            MarketingView, MarketingEdit,
            NotificationsView, NotificationsEdit,
            DeliverySettingsView, DeliverySettingsEdit
        ];
    }

    public static class Vendor
    {
        public const string AccountView = "vendor_account.view";
        public const string AccountEdit = "vendor_account.edit";
        public const string DashboardView = "vendor_dashboard.view";
        public const string OrdersView = "vendor_orders.view";
        public const string OrdersEdit = "vendor_orders.edit";
        public const string OrdersApprove = "vendor_orders.approve";
        public const string CatalogView = "vendor_catalog.view";
        public const string CatalogCreate = "vendor_catalog.create";
        public const string CatalogEdit = "vendor_catalog.edit";
        public const string CatalogApprove = "vendor_catalog.approve";
        public const string BranchTeamView = "vendor_branch_team.view";
        public const string BranchTeamCreate = "vendor_branch_team.create";
        public const string BranchTeamEdit = "vendor_branch_team.edit";
        public const string BranchTeamApprove = "vendor_branch_team.approve";
        public const string FinanceView = "vendor_finance.view";
        public const string FinanceExport = "vendor_finance.export";
        public const string SupportView = "vendor_support.view";
        public const string SupportEdit = "vendor_support.edit";
        public const string SettingsView = "vendor_settings.view";
        public const string SettingsEdit = "vendor_settings.edit";
        public const string NotificationsView = "vendor_notifications.view";
        public const string NotificationsEdit = "vendor_notifications.edit";

        public static readonly string[] Owner =
        [
            AccountView, AccountEdit, DashboardView,
            OrdersView, OrdersEdit, OrdersApprove,
            CatalogView, CatalogCreate, CatalogEdit, CatalogApprove,
            BranchTeamView, BranchTeamCreate, BranchTeamEdit, BranchTeamApprove,
            FinanceView, FinanceExport,
            SupportView, SupportEdit,
            SettingsView, SettingsEdit,
            NotificationsView, NotificationsEdit
        ];

        public static readonly string[] BranchManager =
        [
            AccountView, AccountEdit, DashboardView,
            OrdersView, OrdersEdit, OrdersApprove,
            CatalogView, CatalogCreate, CatalogEdit,
            BranchTeamView,
            FinanceView,
            SupportView, SupportEdit,
            SettingsView, SettingsEdit,
            NotificationsView, NotificationsEdit
        ];

        public static readonly string[] BranchStaff =
        [
            AccountView, AccountEdit, DashboardView,
            OrdersView, OrdersEdit,
            CatalogView, CatalogEdit,
            SupportView,
            SettingsView,
            NotificationsView, NotificationsEdit
        ];
    }

    public static class Driver
    {
        public const string AccountView = "driver_account.view";
        public const string AccountEdit = "driver_account.edit";
        public const string DashboardView = "driver_dashboard.view";
        public const string ProfileView = "driver_profile.view";
        public const string ProfileEdit = "driver_profile.edit";
        public const string DeliveriesView = "driver_deliveries.view";
        public const string DeliveriesEdit = "driver_deliveries.edit";
        public const string DeliveriesApprove = "driver_deliveries.approve";
        public const string AvailabilityEdit = "driver_availability.edit";
        public const string LocationEdit = "driver_location.edit";
        public const string WalletView = "driver_wallet.view";
        public const string WalletEdit = "driver_wallet.edit";
        public const string SupportView = "driver_support.view";
        public const string SupportEdit = "driver_support.edit";
        public const string NotificationsView = "driver_notifications.view";
        public const string NotificationsEdit = "driver_notifications.edit";

        public static readonly string[] All =
        [
            AccountView, AccountEdit, DashboardView,
            ProfileView, ProfileEdit,
            DeliveriesView, DeliveriesEdit, DeliveriesApprove,
            AvailabilityEdit, LocationEdit,
            WalletView, WalletEdit,
            SupportView, SupportEdit,
            NotificationsView, NotificationsEdit
        ];
    }

    public static class Customer
    {
        public const string AccountView = "customer_account.view";
        public const string AccountEdit = "customer_account.edit";
        public const string ProfileView = "customer_profile.view";
        public const string ProfileEdit = "customer_profile.edit";
        public const string AddressesView = "customer_addresses.view";
        public const string AddressesEdit = "customer_addresses.edit";
        public const string OrdersView = "customer_orders.view";
        public const string OrdersCreate = "customer_orders.create";
        public const string OrdersEdit = "customer_orders.edit";
        public const string CheckoutView = "customer_checkout.view";
        public const string CheckoutEdit = "customer_checkout.edit";
        public const string NotificationsView = "customer_notifications.view";
        public const string NotificationsEdit = "customer_notifications.edit";

        public static readonly string[] All =
        [
            AccountView, AccountEdit,
            ProfileView, ProfileEdit,
            AddressesView, AddressesEdit,
            OrdersView, OrdersCreate, OrdersEdit,
            CheckoutView, CheckoutEdit,
            NotificationsView, NotificationsEdit
        ];
    }
}
