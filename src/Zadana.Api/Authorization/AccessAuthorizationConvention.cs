using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Zadana.Domain.Modules.Identity.Constants;

namespace Zadana.Api.Authorization;

public sealed class AccessAuthorizationConvention : IApplicationModelConvention
{
    private static readonly IReadOnlyDictionary<string, ControllerAccessRule> Rules =
        new Dictionary<string, ControllerAccessRule>(StringComparer.Ordinal)
        {
            ["AdminAuth"] = new(
                [PermissionKeys.Admin.AccountView],
                edit: [PermissionKeys.Admin.AccountEdit],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Logout"] = [],
                    ["GetCurrentUser"] = [],
                    ["ChangeTemporaryPassword"] = [],
                    ["ChangePassword"] = [],
                    ["UpdateCurrentUser"] = [PermissionKeys.Admin.AccountEdit]
                }),
            ["AdminAccess"] = new(
                [PermissionKeys.Admin.UsersAccessView],
                create: [PermissionKeys.Admin.UsersAccessCreate],
                edit: [PermissionKeys.Admin.UsersAccessEdit]),
            ["AdminDashboard"] = new([PermissionKeys.Admin.DashboardView]),
            ["AdminGeography"] = new([PermissionKeys.Admin.DashboardView]),
            ["AdminBrands"] = CreateCatalogAdminRule(),
            ["AdminBrandRequests"] = CreateCatalogAdminRule(),
            ["AdminCatalogRequestCenter"] = CreateCatalogAdminRule(),
            ["AdminCategories"] = CreateCatalogAdminRule(),
            ["AdminCategoryRequests"] = CreateCatalogAdminRule(),
            ["AdminMasterProducts"] = CreateCatalogAdminRule(),
            ["AdminProductRequests"] = CreateCatalogAdminRule(),
            ["AdminProductTypes"] = CreateCatalogAdminRule(),
            ["AdminUnits"] = CreateCatalogAdminRule(),
            ["AdminDeliveryPricing"] = new(
                [PermissionKeys.Admin.DeliverySettingsView],
                edit: [PermissionKeys.Admin.DeliverySettingsEdit]),
            ["AdminDeliveryZones"] = new(
                [PermissionKeys.Admin.DeliverySettingsView],
                create: [PermissionKeys.Admin.DeliverySettingsEdit],
                edit: [PermissionKeys.Admin.DeliverySettingsEdit]),
            ["AdminDrivers"] = new(
                [PermissionKeys.Admin.DriversView],
                edit: [PermissionKeys.Admin.DriversEdit],
                approve: [PermissionKeys.Admin.DriversApprove],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["BanDriver"] = [PermissionKeys.Admin.DriversApprove],
                    ["UnbanDriver"] = [PermissionKeys.Admin.DriversApprove]
                }),
            ["AdminEmailCenter"] = new(
                [PermissionKeys.Admin.EmailCenterView],
                edit: [PermissionKeys.Admin.EmailCenterEdit],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["GetOverview"] = [PermissionKeys.Admin.EmailCenterView],
                    ["GetDispatches"] = [PermissionKeys.Admin.EmailCenterView],
                    ["ResolveRecipients"] = [PermissionKeys.Admin.EmailCenterView],
                    ["UpdateRule"] = [PermissionKeys.Admin.EmailCenterEdit],
                    ["TestSend"] = [PermissionKeys.Admin.EmailCenterEdit]
                }),
            ["AdminFinances"] = new(
                [PermissionKeys.Admin.FinancesView],
                create: [PermissionKeys.Admin.FinancesEdit],
                edit: [PermissionKeys.Admin.FinancesEdit],
                approve: [PermissionKeys.Admin.FinancesApprove]),
            ["AdminFinanceAdjustments"] = new(
                [PermissionKeys.Admin.FinancesView],
                create: [PermissionKeys.Admin.FinancesEdit],
                edit: [PermissionKeys.Admin.FinancesEdit],
                approve: [PermissionKeys.Admin.FinancesApprove]),
            ["AdminFinanceRefunds"] = new(
                [PermissionKeys.Admin.FinancesView],
                edit: [PermissionKeys.Admin.FinancesEdit],
                approve: [PermissionKeys.Admin.FinancesApprove]),
            ["AdminFinanceStatements"] = new(
                [PermissionKeys.Admin.FinancesView],
                edit: [PermissionKeys.Admin.FinancesEdit],
                approve: [PermissionKeys.Admin.FinancesApprove]),
            ["AdminPayouts"] = new(
                [PermissionKeys.Admin.FinancesView],
                edit: [PermissionKeys.Admin.FinancesEdit],
                approve: [PermissionKeys.Admin.FinancesApprove],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Trigger"] = [PermissionKeys.Admin.FinancesApprove],
                    ["Retry"] = [PermissionKeys.Admin.FinancesApprove],
                    ["Cancel"] = [PermissionKeys.Admin.FinancesApprove],
                    ["MarkPaid"] = [PermissionKeys.Admin.FinancesApprove],
                    ["ConfirmManual"] = [PermissionKeys.Admin.FinancesApprove],
                    ["ClaimManual"] = [PermissionKeys.Admin.FinancesApprove],
                    ["RecordManualBankSubmission"] = [PermissionKeys.Admin.FinancesApprove],
                    ["ReleaseManualClaim"] = [PermissionKeys.Admin.FinancesApprove],
                    ["RecordReturn"] = [PermissionKeys.Admin.FinancesApprove],
                    ["GetProcessingSettings"] = [PermissionKeys.Admin.FinancesManageSettings],
                    ["UpdateProcessingSettings"] = [PermissionKeys.Admin.FinancesManageSettings],
                    ["GetProcessingSettingsAudit"] = [PermissionKeys.Admin.FinancesManageSettings]
                }),
            ["BankTransfer"] = new(
                [PermissionKeys.Admin.FinancesView],
                edit: [PermissionKeys.Admin.FinancesEdit],
                approve: [PermissionKeys.Admin.FinancesApprove],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["ConfirmBankTransfer"] = [PermissionKeys.Admin.FinancesApprove],
                    ["RejectBankTransfer"] = [PermissionKeys.Admin.FinancesApprove],
                }),
            ["AdminSettlements"] = new(
                [PermissionKeys.Admin.FinancesView],
                edit: [PermissionKeys.Admin.FinancesEdit],
                approve: [PermissionKeys.Admin.FinancesApprove]),
            ["AdminPayoutReconciliation"] = new(
                [PermissionKeys.Admin.FinancesView],
                edit: [PermissionKeys.Admin.FinancesEdit],
                approve: [PermissionKeys.Admin.FinancesApprove],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["ImportBankStatement"] = [PermissionKeys.Admin.FinancesApprove],
                    ["MatchEntry"] = [PermissionKeys.Admin.FinancesApprove],
                    ["IgnoreEntry"] = [PermissionKeys.Admin.FinancesApprove]
                }),
            ["AdminCustomers"] = new(
                [PermissionKeys.Admin.CustomersView],
                edit: [PermissionKeys.Admin.CustomersEdit]),
            ["AdminMarketingBanners"] = new(
                [PermissionKeys.Admin.MarketingView],
                create: [PermissionKeys.Admin.MarketingEdit],
                edit: [PermissionKeys.Admin.MarketingEdit]),
            ["AdminMarketingCoupons"] = new(
                [PermissionKeys.Admin.MarketingView],
                create: [PermissionKeys.Admin.MarketingEdit],
                edit: [PermissionKeys.Admin.MarketingEdit]),
            ["AdminMarketingFeaturedProducts"] = new(
                [PermissionKeys.Admin.MarketingView],
                create: [PermissionKeys.Admin.MarketingEdit],
                edit: [PermissionKeys.Admin.MarketingEdit]),
            ["AdminMarketingHomeContentSections"] = new(
                [PermissionKeys.Admin.MarketingView],
                edit: [PermissionKeys.Admin.MarketingEdit]),
            ["AdminMarketingHomeSections"] = new(
                [PermissionKeys.Admin.MarketingView],
                create: [PermissionKeys.Admin.MarketingEdit],
                edit: [PermissionKeys.Admin.MarketingEdit]),
            ["AdminMarketingProductCardPriceVisibility"] = new(
                [PermissionKeys.Admin.MarketingView],
                edit: [PermissionKeys.Admin.MarketingEdit]),
            ["AdminOrderCases"] = new(
                [PermissionKeys.Admin.DisputesView],
                create: [PermissionKeys.Admin.DisputesEdit],
                edit: [PermissionKeys.Admin.DisputesEdit],
                approve: [PermissionKeys.Admin.DisputesApprove]),
            ["AdminOrders"] = new(
                [PermissionKeys.Admin.OrdersView],
                edit: [PermissionKeys.Admin.OrdersEdit],
                approve: [PermissionKeys.Admin.OrdersApprove]),
            ["AdminNotifications"] = new(
                [PermissionKeys.Admin.NotificationsView],
                edit: [PermissionKeys.Admin.NotificationsEdit]),
            ["AdminVendors"] = new(
                [PermissionKeys.Admin.VendorsView],
                create: [PermissionKeys.Admin.VendorsEdit],
                edit: [PermissionKeys.Admin.VendorsEdit],
                approve: [PermissionKeys.Admin.VendorsApprove],
                // Finance operations remain available from the vendor detail
                // screen for convenience, but they must never inherit the
                // comparatively broad VendorsEdit/VendorsApprove permissions.
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["CreateVendorSettlement"] = [PermissionKeys.Admin.FinancesEdit],
                    ["RetryVendorPayout"] = [PermissionKeys.Admin.FinancesApprove],
                    ["CompleteVendorPayout"] = [PermissionKeys.Admin.FinancesApprove],
                    ["SuspendVendorPayout"] = [PermissionKeys.Admin.FinancesApprove],
                    ["EscalateVendorPayout"] = [PermissionKeys.Admin.FinancesApprove]
                }),
            ["AdminVendorWorkspaceState"] = new(
                [PermissionKeys.Admin.VendorsView],
                edit: [PermissionKeys.Admin.VendorsEdit]),
            ["AdminVendorSupportTickets"] = new(
                [PermissionKeys.Admin.DisputesView, PermissionKeys.Admin.VendorsView],
                create: [PermissionKeys.Admin.DisputesEdit, PermissionKeys.Admin.VendorsEdit],
                edit: [PermissionKeys.Admin.DisputesEdit, PermissionKeys.Admin.VendorsEdit]),
            ["AdminVendorBankAccounts"] = new(
                [PermissionKeys.Admin.VendorsView],
                edit: [PermissionKeys.Admin.VendorsEdit],
                approve: [PermissionKeys.Admin.VendorsApprove]),
            ["AdminVendorBranches"] = new(
                [PermissionKeys.Admin.VendorsView],
                create: [PermissionKeys.Admin.VendorsEdit],
                edit: [PermissionKeys.Admin.VendorsEdit],
                approve: [PermissionKeys.Admin.VendorsApprove]),
            ["AdminWallets"] = new(
                [PermissionKeys.Admin.WalletsView],
                create: [PermissionKeys.Admin.WalletsEdit],
                edit: [PermissionKeys.Admin.WalletsEdit],
                approve: [PermissionKeys.Admin.WalletsApprove],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["GetPlatformAccount"] = [PermissionKeys.Admin.FinancesManageSettings],
                    ["UpsertPlatformAccount"] = [PermissionKeys.Admin.FinancesManageSettings, PermissionKeys.Admin.FinancesEdit],
                    ["CreateMoyasarPayoutSource"] = [PermissionKeys.Admin.FinancesManageSettings, PermissionKeys.Admin.FinancesApprove]
                }),
            ["AdminSystemLogs"] = new([PermissionKeys.Admin.SystemView]),
            ["VendorAuth"] = new(
                [PermissionKeys.Vendor.AccountView],
                edit: [PermissionKeys.Vendor.AccountEdit],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Logout"] = [],
                    ["GetCurrentUser"] = [],
                    ["UpdateCurrentUser"] = [PermissionKeys.Vendor.AccountEdit]
                }),
            ["VendorAccess"] = new(
                [PermissionKeys.Vendor.BranchTeamView],
                create: [PermissionKeys.Vendor.BranchTeamCreate],
                edit: [PermissionKeys.Vendor.BranchTeamEdit]),
            ["VendorCatalog"] = new(
                [PermissionKeys.Vendor.CatalogView],
                create: [PermissionKeys.Vendor.CatalogCreate],
                edit: [PermissionKeys.Vendor.CatalogEdit],
                approve: [PermissionKeys.Vendor.CatalogApprove]),
            ["VendorProducts"] = new(
                [PermissionKeys.Vendor.CatalogView],
                create: [PermissionKeys.Vendor.CatalogCreate],
                edit: [PermissionKeys.Vendor.CatalogEdit],
                approve: [PermissionKeys.Vendor.CatalogApprove]),
            ["VendorBrandRequests"] = new(
                [PermissionKeys.Vendor.CatalogView],
                create: [PermissionKeys.Vendor.CatalogCreate]),
            ["VendorCategoryRequests"] = new(
                [PermissionKeys.Vendor.CatalogView],
                create: [PermissionKeys.Vendor.CatalogCreate]),
            ["VendorProductRequests"] = new(
                [PermissionKeys.Vendor.CatalogView],
                create: [PermissionKeys.Vendor.CatalogCreate],
                edit: [PermissionKeys.Vendor.CatalogEdit]),
            ["VendorOrders"] = new(
                [PermissionKeys.Vendor.OrdersView],
                edit: [PermissionKeys.Vendor.OrdersEdit],
                approve: [PermissionKeys.Vendor.OrdersApprove]),
            ["VendorCoupons"] = new(
                [PermissionKeys.Vendor.OffersView],
                create: [PermissionKeys.Vendor.OffersEdit],
                edit: [PermissionKeys.Vendor.OffersEdit]),
            ["VendorOrderCases"] = new(
                [PermissionKeys.Vendor.SupportView],
                create: [PermissionKeys.Vendor.SupportEdit],
                edit: [PermissionKeys.Vendor.SupportEdit]),
            ["VendorSupportTickets"] = new(
                [PermissionKeys.Vendor.SupportView],
                create: [PermissionKeys.Vendor.SupportEdit],
                edit: [PermissionKeys.Vendor.SupportEdit]),
            ["VendorWorkspace"] = new([PermissionKeys.Vendor.DashboardView]),
            ["VendorWorkspaceState"] = new(
                [PermissionKeys.Vendor.DashboardView],
                edit: [PermissionKeys.Vendor.SettingsEdit]),
            ["Vendors"] = new(
                [PermissionKeys.Vendor.ProfileView],
                create: [PermissionKeys.Vendor.ProfileEdit],
                edit: [PermissionKeys.Vendor.ProfileEdit]),
            ["VendorNotifications"] = new(
                [PermissionKeys.Vendor.NotificationsView],
                edit: [PermissionKeys.Vendor.NotificationsView]),
            ["DriverAuth"] = new(
                [PermissionKeys.Driver.AccountView],
                edit: [PermissionKeys.Driver.AccountEdit],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Logout"] = [],
                    ["GetCurrentUser"] = [],
                    ["UpdateCurrentUser"] = [PermissionKeys.Driver.AccountEdit]
                }),
            ["Drivers"] = new(
                [PermissionKeys.Driver.DeliveriesView],
                edit: [PermissionKeys.Driver.DeliveriesEdit],
                approve: [PermissionKeys.Driver.DeliveriesApprove],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["GetMyStatus"] = [PermissionKeys.Driver.AccountView],
                    ["GetHome"] = [PermissionKeys.Driver.DashboardView],
                    ["SetAvailability"] = [PermissionKeys.Driver.AvailabilityEdit],
                    ["UpdateLocation"] = [PermissionKeys.Driver.LocationEdit]
                }),
            ["DriverProfile"] = new(
                [PermissionKeys.Driver.ProfileView],
                edit: [PermissionKeys.Driver.ProfileEdit]),
            ["DriverWallet"] = new(
                [PermissionKeys.Driver.WalletView],
                create: [PermissionKeys.Driver.WalletEdit],
                edit: [PermissionKeys.Driver.WalletEdit]),
            ["DriverSupport"] = new(
                [PermissionKeys.Driver.SupportView],
                create: [PermissionKeys.Driver.SupportEdit],
                edit: [PermissionKeys.Driver.SupportEdit]),
            ["DriverNotifications"] = new(
                [PermissionKeys.Driver.NotificationsView],
                edit: [PermissionKeys.Driver.NotificationsEdit]),
            ["CustomerAuth"] = new(
                [PermissionKeys.Customer.AccountView],
                edit: [PermissionKeys.Customer.AccountEdit],
                overrides: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["Logout"] = [],
                    ["GetCurrentUser"] = [],
                    ["UpdateCurrentUser"] = [PermissionKeys.Customer.AccountEdit]
                }),
            ["CustomerAddresses"] = new(
                [PermissionKeys.Customer.AddressesView],
                create: [PermissionKeys.Customer.AddressesEdit],
                edit: [PermissionKeys.Customer.AddressesEdit]),
            ["Cart"] = new(
                [PermissionKeys.Customer.CheckoutView],
                create: [PermissionKeys.Customer.CheckoutEdit],
                edit: [PermissionKeys.Customer.CheckoutEdit]),
            ["Checkout"] = new(
                [PermissionKeys.Customer.CheckoutView],
                create: [PermissionKeys.Customer.CheckoutEdit],
                edit: [PermissionKeys.Customer.CheckoutEdit]),
            ["Orders"] = new(
                [PermissionKeys.Customer.OrdersView],
                create: [PermissionKeys.Customer.OrdersCreate],
                edit: [PermissionKeys.Customer.OrdersEdit]),
            ["Notifications"] = new(
                [PermissionKeys.Customer.NotificationsView],
                edit: [PermissionKeys.Customer.NotificationsEdit]),
            ["NotificationDevices"] = new(
                [
                    PermissionKeys.Admin.NotificationsView,
                    PermissionKeys.Vendor.NotificationsView,
                    PermissionKeys.Driver.NotificationsView,
                    PermissionKeys.Customer.NotificationsView
                ],
                create:
                [
                    PermissionKeys.Admin.NotificationsEdit,
                    PermissionKeys.Vendor.NotificationsEdit,
                    PermissionKeys.Driver.NotificationsEdit,
                    PermissionKeys.Customer.NotificationsEdit
                ],
                edit:
                [
                    PermissionKeys.Admin.NotificationsEdit,
                    PermissionKeys.Vendor.NotificationsEdit,
                    PermissionKeys.Driver.NotificationsEdit,
                    PermissionKeys.Customer.NotificationsEdit
                ])
        };

    public void Apply(ApplicationModel application)
    {
        var missingMappings = new List<string>();

        foreach (var controller in application.Controllers)
        {
            var controllerRequiresAuthorization = controller.Attributes.OfType<AuthorizeAttribute>().Any();
            var actionRequiresAuthorization = controller.Actions.Any(action =>
                action.Attributes.OfType<AuthorizeAttribute>().Any());

            if (!controllerRequiresAuthorization && !actionRequiresAuthorization)
            {
                continue;
            }

            if (!Rules.TryGetValue(controller.ControllerName, out var rule))
            {
                missingMappings.Add(controller.ControllerName);
                continue;
            }

            foreach (var action in controller.Actions)
            {
                if (action.Attributes.OfType<AllowAnonymousAttribute>().Any())
                {
                    continue;
                }

                if (!(controllerRequiresAuthorization || action.Attributes.OfType<AuthorizeAttribute>().Any()))
                {
                    continue;
                }

                var permissions = ResolvePermissions(rule, action);
                if (permissions.Length == 0)
                {
                    continue;
                }

                action.Filters.Add(new RequireAccessAttribute(permissions));
            }
        }

        if (missingMappings.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing access-control mappings for authorized controllers: {string.Join(", ", missingMappings.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))}");
        }
    }

    private static string[] ResolvePermissions(ControllerAccessRule rule, ActionModel action)
    {
        if (rule.Overrides.TryGetValue(action.ActionName, out var overridePermissions))
        {
            return overridePermissions;
        }

        var actionDescriptor = $"{action.ActionName} {GetRouteTemplate(action)}";
        if (ContainsAny(actionDescriptor, "search"))
        {
            return rule.Read;
        }

        var methods = GetHttpMethods(action);
        if (methods.Count == 0 || methods.All(method => HttpMethods.IsGet(method) || HttpMethods.IsHead(method)))
        {
            return rule.Read;
        }

        if (ContainsAny(actionDescriptor, "approve", "reject", "review", "activate", "deactivate", "process", "confirm", "verify", "suspend", "reactivate", "archive", "lock", "unlock", "escalate", "resolve", "mark"))
        {
            return rule.Approve;
        }

        if (methods.Any(HttpMethods.IsDelete) || methods.Any(HttpMethods.IsPut) || methods.Any(HttpMethods.IsPatch))
        {
            return rule.Edit;
        }

        if (methods.Any(HttpMethods.IsPost))
        {
            if (ContainsAny(actionDescriptor, "create", "add", "register", "upload", "place", "submit", "apply", "reply", "message", "bulk"))
            {
                return rule.Create;
            }

            return rule.Edit.Length > 0 ? rule.Edit : rule.Create;
        }

        return rule.Read;
    }

    private static HashSet<string> GetHttpMethods(ActionModel action)
    {
        return action.Selectors
            .SelectMany(selector => selector.ActionConstraints ?? [])
            .OfType<HttpMethodActionConstraint>()
            .SelectMany(constraint => constraint.HttpMethods)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetRouteTemplate(ActionModel action)
    {
        return string.Join(' ', action.Selectors
            .Select(selector => selector.AttributeRouteModel?.Template)
            .Where(template => !string.IsNullOrWhiteSpace(template)));
    }

    private static bool ContainsAny(string source, params string[] keywords)
    {
        return keywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static ControllerAccessRule CreateCatalogAdminRule()
    {
        return new ControllerAccessRule(
            [PermissionKeys.Admin.CatalogView],
            create: [PermissionKeys.Admin.CatalogCreate],
            edit: [PermissionKeys.Admin.CatalogEdit],
            approve: [PermissionKeys.Admin.CatalogApprove]);
    }

    private sealed class ControllerAccessRule
    {
        public ControllerAccessRule(
            string[] read,
            string[]? create = null,
            string[]? edit = null,
            string[]? approve = null,
            Dictionary<string, string[]>? overrides = null)
        {
            Read = read;
            Create = create ?? read;
            Edit = edit ?? Create;
            Approve = approve ?? Edit;
            Overrides = overrides ?? new Dictionary<string, string[]>(StringComparer.Ordinal);
        }

        public string[] Read { get; }
        public string[] Create { get; }
        public string[] Edit { get; }
        public string[] Approve { get; }
        public Dictionary<string, string[]> Overrides { get; }
    }
}
