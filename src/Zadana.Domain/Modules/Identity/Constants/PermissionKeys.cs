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
        public const string VendorsExport = "vendors.export";
        public const string CatalogView = "catalog.view";
        public const string CatalogCreate = "catalog.create";
        public const string CatalogEdit = "catalog.edit";
        public const string CatalogApprove = "catalog.approve";
        public const string CatalogExport = "catalog.export";
        public const string OrdersView = "orders.view";
        public const string OrdersEdit = "orders.edit";
        public const string OrdersApprove = "orders.approve";
        public const string OrdersExport = "orders.export";
        public const string CustomersView = "customers.view";
        public const string CustomersEdit = "customers.edit";
        public const string CustomersExport = "customers.export";
        public const string DriversView = "drivers.view";
        public const string DriversEdit = "drivers.edit";
        public const string DriversApprove = "drivers.approve";
        public const string DriversExport = "drivers.export";
        public const string DisputesView = "disputes.view";
        public const string DisputesEdit = "disputes.edit";
        public const string DisputesApprove = "disputes.approve";
        public const string DisputesExport = "disputes.export";
        public const string FinancesView = "finances.view";
        public const string FinancesEdit = "finances.edit";
        public const string FinancesApprove = "finances.approve";
        public const string FinancesExport = "finances.export";
        public const string FinancesManageSettings = "finances.manage_settings";
        public const string WalletsView = "wallets.view";
        public const string WalletsEdit = "wallets.edit";
        public const string WalletsApprove = "wallets.approve";
        public const string UsersAccessView = "users_access.view";
        public const string UsersAccessCreate = "users_access.create";
        public const string UsersAccessEdit = "users_access.edit";
        public const string UsersAccessApprove = "users_access.approve";
        public const string UsersAccessManageSettings = "users_access.manage_settings";
        public const string EmailCenterView = "email_center.view";
        public const string EmailCenterEdit = "email_center.edit";
        public const string EmailCenterApprove = "email_center.approve";
        public const string EmailCenterManageSettings = "email_center.manage_settings";
        public const string MarketingView = "marketing.view";
        public const string MarketingCreate = "marketing.create";
        public const string MarketingEdit = "marketing.edit";
        public const string MarketingApprove = "marketing.approve";
        public const string MarketingManageSettings = "marketing.manage_settings";
        public const string NotificationsView = "admin_notifications.view";
        public const string NotificationsEdit = "admin_notifications.edit";
        public const string DeliverySettingsView = "delivery_settings.view";
        public const string DeliverySettingsEdit = "delivery_settings.edit";
        public const string SystemView = "system.view";
        public const string SystemEdit = "system.edit";
        public const string SystemManageSettings = "system.manage_settings";

        public static readonly string[] All =
        [
            AccountView, AccountEdit, DashboardView, DashboardExport,
            VendorsView, VendorsEdit, VendorsApprove, VendorsExport,
            CatalogView, CatalogCreate, CatalogEdit, CatalogApprove, CatalogExport,
            OrdersView, OrdersEdit, OrdersApprove, OrdersExport,
            CustomersView, CustomersEdit, CustomersExport,
            DriversView, DriversEdit, DriversApprove, DriversExport,
            DisputesView, DisputesEdit, DisputesApprove, DisputesExport,
            FinancesView, FinancesEdit, FinancesApprove, FinancesExport, FinancesManageSettings,
            WalletsView, WalletsEdit, WalletsApprove,
            UsersAccessView, UsersAccessCreate, UsersAccessEdit, UsersAccessApprove, UsersAccessManageSettings,
            EmailCenterView, EmailCenterEdit, EmailCenterApprove, EmailCenterManageSettings,
            MarketingView, MarketingCreate, MarketingEdit, MarketingApprove, MarketingManageSettings,
            NotificationsView, NotificationsEdit,
            DeliverySettingsView, DeliverySettingsEdit,
            SystemView, SystemEdit, SystemManageSettings
        ];

        public static readonly string[] Operations =
        [
            AccountView, AccountEdit, DashboardView, DashboardExport,
            VendorsView, VendorsEdit, VendorsApprove, VendorsExport,
            CatalogView, CatalogCreate, CatalogEdit, CatalogApprove, CatalogExport,
            OrdersView, OrdersEdit, OrdersApprove, OrdersExport,
            CustomersView, CustomersEdit, CustomersExport,
            DriversView, DriversEdit, DriversApprove, DriversExport,
            DisputesView, DisputesEdit, DisputesApprove, DisputesExport,
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
        public const string OrdersExport = "vendor_orders.export";
        public const string CatalogView = "vendor_catalog.view";
        public const string CatalogCreate = "vendor_catalog.create";
        public const string CatalogEdit = "vendor_catalog.edit";
        public const string CatalogApprove = "vendor_catalog.approve";
        public const string CatalogExport = "vendor_catalog.export";
        public const string BranchTeamView = "vendor_branch_team.view";
        public const string BranchTeamCreate = "vendor_branch_team.create";
        public const string BranchTeamEdit = "vendor_branch_team.edit";
        public const string BranchTeamApprove = "vendor_branch_team.approve";
        public const string BranchTeamManageSettings = "vendor_branch_team.manage_settings";
        public const string FinanceView = "vendor_finance.view";
        public const string FinanceEdit = "vendor_finance.edit";
        public const string FinanceExport = "vendor_finance.export";
        public const string FinanceManageSettings = "vendor_finance.manage_settings";
        public const string SupportView = "vendor_support.view";
        public const string SupportEdit = "vendor_support.edit";
        public const string SupportExport = "vendor_support.export";
        public const string SettingsView = "vendor_settings.view";
        public const string SettingsEdit = "vendor_settings.edit";
        public const string SettingsManageSettings = "vendor_settings.manage_settings";
        public const string NotificationsView = "vendor_notifications.view";
        public const string NotificationsEdit = "vendor_notifications.edit";
        public const string OffersView = "vendor_offers.view";
        public const string OffersEdit = "vendor_offers.edit";
        public const string DisputesView = "vendor_disputes.view";
        public const string DisputesEdit = "vendor_disputes.edit";
        public const string AlertsView = "vendor_alerts.view";
        public const string AlertsEdit = "vendor_alerts.edit";
        public const string StaffView = "vendor_staff.view";
        public const string StaffEdit = "vendor_staff.edit";
        public const string ProfileView = "vendor_profile.view";
        public const string ProfileEdit = "vendor_profile.edit";

        public static readonly string[] SessionBaseline =
        [
            AccountView,
            ProfileView,
            NotificationsView
        ];

        public static readonly string[] Owner =
        [
            AccountView, AccountEdit, DashboardView,
            OrdersView, OrdersEdit, OrdersApprove, OrdersExport,
            CatalogView, CatalogCreate, CatalogEdit, CatalogApprove, CatalogExport,
            BranchTeamView, BranchTeamCreate, BranchTeamEdit, BranchTeamApprove, BranchTeamManageSettings,
            FinanceView, FinanceEdit, FinanceExport, FinanceManageSettings,
            SupportView, SupportEdit, SupportExport,
            SettingsView, SettingsEdit, SettingsManageSettings,
            NotificationsView, NotificationsEdit,
            OffersView, OffersEdit,
            DisputesView, DisputesEdit,
            AlertsView, AlertsEdit,
            StaffView, StaffEdit,
            ProfileView, ProfileEdit
        ];

        public static readonly string[] BranchManager =
        [
            AccountView, AccountEdit, DashboardView,
            OrdersView, OrdersEdit, OrdersApprove, OrdersExport,
            CatalogView, CatalogCreate, CatalogEdit, CatalogExport,
            BranchTeamView,
            FinanceView,
            SupportView, SupportEdit, SupportExport,
            SettingsView, SettingsEdit,
            NotificationsView, NotificationsEdit,
            OffersView, OffersEdit,
            DisputesView, DisputesEdit,
            AlertsView, AlertsEdit,
            StaffView,
            ProfileView, ProfileEdit
        ];

        public static readonly string[] BranchStaff =
        [
            AccountView, AccountEdit, DashboardView,
            OrdersView, OrdersEdit,
            CatalogView, CatalogEdit,
            SupportView,
            SettingsView,
            NotificationsView, NotificationsEdit,
            OffersView,
            DisputesView,
            AlertsView,
            ProfileView, ProfileEdit
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
