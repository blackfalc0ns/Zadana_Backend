using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Persistence;

public class ApplicationDbContextInitialiser
{
    private const string DefaultAdminEmail = "admin@system.com";
    private const string DefaultAdminPassword = "Admin@123";

    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public ApplicationDbContextInitialiser(
        ApplicationDbContext context,
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitialiseAsync()
    {
        if (_context.Database.IsSqlServer())
        {
            await _context.Database.MigrateAsync();
        }
    }

    public async Task SeedAsync()
    {
        await TrySeedAsync();
    }

    public async Task<DevelopmentSeedSummary> ResetAndSeedAsync()
    {
        await ResetDevelopmentDataAsync();
        await TrySeedAsync();
        return await BuildSummaryAsync();
    }

    private async Task TrySeedAsync()
    {
        await SeedIdentityRolesAsync();
        await SeedAdminAccessControlAsync();
        await SeedSuperAdminAsync();
        await SeedSuperAdminAccessScopeAsync();
    }

    private async Task SeedIdentityRolesAsync()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            if (!await _roleManager.RoleExistsAsync(role.ToString()))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role.ToString()));
            }
        }
    }

    private async Task SeedAdminAccessControlAsync()
    {
        var permissions = BuildPermissionKeys()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(CreatePermissionSeed)
            .ToList();

        foreach (var seed in permissions)
        {
            var existing = await _context.PermissionDefinitions.FirstOrDefaultAsync(x => x.Key == seed.Key);
            if (existing is null)
            {
                _context.PermissionDefinitions.Add(new PermissionDefinition(
                    seed.Key,
                    seed.Name,
                    seed.Domain,
                    seed.Action,
                    seed.PanelScope,
                    seed.Description,
                    seed.IsSensitive));
                continue;
            }

            existing.Update(seed.Name, seed.Domain, seed.Action, seed.PanelScope, seed.Description, seed.IsSensitive);
        }

        await _context.SaveChangesAsync();

        foreach (var roleSeed in BuildRoleSeeds())
        {
            await UpsertRoleSeedAsync(roleSeed);
        }
    }

    private async Task UpsertRoleSeedAsync(RoleSeed seed)
    {
        var existingRole = await _context.RoleDefinitions
            .Include(x => x.RolePermissions)
            .FirstOrDefaultAsync(x => x.Code == seed.Code);

        if (existingRole is null)
        {
            existingRole = new RoleDefinition(
                seed.Code,
                seed.Name,
                seed.IdentityRole,
                seed.PanelScope,
                description: seed.Description);
            _context.RoleDefinitions.Add(existingRole);
            await _context.SaveChangesAsync();
        }
        else
        {
            existingRole.Update(
                seed.Name,
                seed.IdentityRole,
                seed.PanelScope,
                isSystem: true,
                isActive: true,
                description: seed.Description);
            await _context.SaveChangesAsync();
        }

        var permissionIds = await _context.PermissionDefinitions
            .Where(x => seed.Permissions.Contains(x.Key))
            .Select(x => x.Id)
            .ToListAsync();

        var desiredPermissionIds = permissionIds.ToHashSet();
        var existingPermissionIds = existingRole.RolePermissions
            .Select(x => x.PermissionDefinitionId)
            .ToHashSet();

        var obsolete = existingRole.RolePermissions
            .Where(x => !desiredPermissionIds.Contains(x.PermissionDefinitionId))
            .ToList();

        if (obsolete.Count > 0)
        {
            _context.RolePermissions.RemoveRange(obsolete);
        }

        foreach (var permissionId in desiredPermissionIds.Where(id => !existingPermissionIds.Contains(id)))
        {
            _context.RolePermissions.Add(new RolePermission(existingRole.Id, permissionId));
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedSuperAdminAsync()
    {
        var admin = await _userManager.FindByEmailAsync(DefaultAdminEmail);
        if (admin is null)
        {
            admin = new User(
                "Super Admin",
                DefaultAdminEmail,
                "01000000000",
                UserRole.SuperAdmin);

            var result = await _userManager.CreateAsync(admin, DefaultAdminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create admin user {DefaultAdminEmail}: {string.Join(", ", result.Errors.Select(x => x.Description))}");
            }
        }

        if (!await _userManager.IsInRoleAsync(admin, UserRole.SuperAdmin.ToString()))
        {
            await _userManager.AddToRoleAsync(admin, UserRole.SuperAdmin.ToString());
        }

        admin.VerifyEmail();
        admin.VerifyPhone();
        _context.Users.Update(admin);
        await _context.SaveChangesAsync();
    }

    private async Task SeedSuperAdminAccessScopeAsync()
    {
        var admin = await _userManager.FindByEmailAsync(DefaultAdminEmail);
        if (admin is null)
        {
            throw new InvalidOperationException($"Admin user {DefaultAdminEmail} was not created.");
        }

        var role = await _context.RoleDefinitions.FirstOrDefaultAsync(x => x.Code == "super_admin_all");
        if (role is null)
        {
            throw new InvalidOperationException("Super Admin role definition was not created.");
        }

        var existing = await _context.UserAccessScopes
            .FirstOrDefaultAsync(x => x.UserId == admin.Id && x.IsActive);

        if (existing is null)
        {
            _context.UserAccessScopes.Add(new UserAccessScope(
                admin.Id,
                role.Id,
                PanelScope.SuperAdminPanel,
                AccessScopeType.Global));
            admin.IncrementPermissionVersion();
            await _context.SaveChangesAsync();
            return;
        }

        if (existing.RoleDefinitionId == role.Id &&
            existing.PanelScope == PanelScope.SuperAdminPanel &&
            existing.ScopeType == AccessScopeType.Global &&
            existing.ScopeEntityId is null)
        {
            return;
        }

        existing.Update(role.Id, PanelScope.SuperAdminPanel, AccessScopeType.Global, null, null);
        admin.IncrementPermissionVersion();
        await _context.SaveChangesAsync();
    }

    private async Task ResetDevelopmentDataAsync()
    {
        _context.ChangeTracker.Clear();

        if (!_context.Database.IsSqlServer())
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.Database.EnsureCreatedAsync();
            _context.ChangeTracker.Clear();
            return;
        }

        await DisableAllTableConstraintsAsync();
        try
        {
            const string sql = """
                DECLARE @sql NVARCHAR(MAX) = N'';

                SELECT @sql += N'DELETE FROM '
                    + QUOTENAME(SCHEMA_NAME(schema_id))
                    + N'.'
                    + QUOTENAME(name)
                    + N';'
                FROM sys.tables
                WHERE name <> N'__EFMigrationsHistory'
                ORDER BY name;

                EXEC sp_executesql @sql;
                """;

            await _context.Database.ExecuteSqlRawAsync(sql);
            _context.ChangeTracker.Clear();
        }
        finally
        {
            await EnableAllTableConstraintsAsync();
        }
    }

    private async Task DisableAllTableConstraintsAsync()
    {
        if (!_context.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            DECLARE @sql NVARCHAR(MAX) = N'';
            SELECT @sql += N'ALTER TABLE '
                + QUOTENAME(SCHEMA_NAME(schema_id))
                + N'.'
                + QUOTENAME(name)
                + N' NOCHECK CONSTRAINT ALL;'
            FROM sys.tables;
            EXEC sp_executesql @sql;
            """;

        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task EnableAllTableConstraintsAsync()
    {
        if (!_context.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            DECLARE @sql NVARCHAR(MAX) = N'';
            SELECT @sql += N'ALTER TABLE '
                + QUOTENAME(SCHEMA_NAME(schema_id))
                + N'.'
                + QUOTENAME(name)
                + N' CHECK CONSTRAINT ALL;'
            FROM sys.tables;
            EXEC sp_executesql @sql;
            """;

        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<DevelopmentSeedSummary> BuildSummaryAsync()
    {
        return new DevelopmentSeedSummary(
            await _context.Categories.CountAsync(),
            await _context.Brands.CountAsync(),
            await _context.MasterProducts.CountAsync(),
            await _context.Vendors.CountAsync(),
            await _context.VendorProducts.CountAsync(),
            await _userManager.Users.CountAsync(x => x.Role == UserRole.Customer),
            await _context.Drivers.CountAsync(),
            await _context.Orders.CountAsync(),
            await _context.HomeBanners.CountAsync(),
            await _context.Coupons.CountAsync(),
            await _context.Reviews.CountAsync(),
            await _context.Notifications.CountAsync());
    }

    private static IReadOnlyList<string> BuildPermissionKeys() =>
    [
        ..PermissionKeys.Admin.All,
        ..PermissionKeys.Vendor.Owner,
        ..PermissionKeys.Vendor.BranchManager,
        ..PermissionKeys.Vendor.BranchStaff,
        ..PermissionKeys.Driver.All,
        ..PermissionKeys.Customer.All
    ];

    private static IReadOnlyList<RoleSeed> BuildRoleSeeds() =>
    [
        new("super_admin_all", "Super Admin", UserRole.SuperAdmin, PanelScope.SuperAdminPanel, PermissionKeys.Admin.All, "Super Admin system role."),
        new("admin_operations", "Operations Lead", UserRole.Admin, PanelScope.SuperAdminPanel, PermissionKeys.Admin.Operations, "Operations admin role."),
        new("risk_admin", "Risk Admin", UserRole.Admin, PanelScope.SuperAdminPanel,
        [
            PermissionKeys.Admin.DashboardView,
            PermissionKeys.Admin.DashboardExport,
            PermissionKeys.Admin.VendorsView,
            PermissionKeys.Admin.VendorsApprove,
            PermissionKeys.Admin.VendorsExport,
            PermissionKeys.Admin.OrdersView,
            PermissionKeys.Admin.OrdersApprove,
            PermissionKeys.Admin.OrdersExport,
            PermissionKeys.Admin.CustomersView,
            PermissionKeys.Admin.CustomersExport,
            PermissionKeys.Admin.DriversView,
            PermissionKeys.Admin.DriversApprove,
            PermissionKeys.Admin.DisputesView,
            PermissionKeys.Admin.DisputesEdit,
            PermissionKeys.Admin.DisputesApprove,
            PermissionKeys.Admin.DisputesExport,
            PermissionKeys.Admin.EmailCenterView,
            PermissionKeys.Admin.EmailCenterApprove,
            PermissionKeys.Admin.NotificationsView,
            PermissionKeys.Admin.NotificationsEdit
        ], "Risk and compliance admin role."),
        new("finance_admin", "Finance Admin", UserRole.Admin, PanelScope.SuperAdminPanel,
        [
            PermissionKeys.Admin.DashboardView,
            PermissionKeys.Admin.DashboardExport,
            PermissionKeys.Admin.OrdersView,
            PermissionKeys.Admin.OrdersExport,
            PermissionKeys.Admin.VendorsView,
            PermissionKeys.Admin.VendorsExport,
            PermissionKeys.Admin.DisputesView,
            PermissionKeys.Admin.DisputesExport,
            PermissionKeys.Admin.FinancesView,
            PermissionKeys.Admin.FinancesEdit,
            PermissionKeys.Admin.FinancesApprove,
            PermissionKeys.Admin.FinancesExport,
            PermissionKeys.Admin.FinancesManageSettings,
            PermissionKeys.Admin.EmailCenterView,
            PermissionKeys.Admin.EmailCenterEdit,
            PermissionKeys.Admin.NotificationsView,
            PermissionKeys.Admin.NotificationsEdit
        ], "Finance admin role."),
        new("support_admin", "Support Admin", UserRole.Admin, PanelScope.SuperAdminPanel,
        [
            PermissionKeys.Admin.DashboardView,
            PermissionKeys.Admin.VendorsView,
            PermissionKeys.Admin.CatalogView,
            PermissionKeys.Admin.OrdersView,
            PermissionKeys.Admin.OrdersExport,
            PermissionKeys.Admin.CustomersView,
            PermissionKeys.Admin.CustomersEdit,
            PermissionKeys.Admin.DriversView,
            PermissionKeys.Admin.DisputesView,
            PermissionKeys.Admin.EmailCenterView,
            PermissionKeys.Admin.NotificationsView,
            PermissionKeys.Admin.NotificationsEdit
        ], "Support admin role."),
        new("vendor_owner", "Vendor Owner", UserRole.Vendor, PanelScope.VendorPanel, PermissionKeys.Vendor.Owner, "Vendor owner role."),
        new("vendor_company_manager", "Vendor Company Manager", UserRole.VendorStaff, PanelScope.VendorPanel, PermissionKeys.Vendor.Owner, "Vendor company manager role."),
        new("vendor_branch_manager", "Vendor Branch Manager", UserRole.VendorStaff, PanelScope.VendorPanel, PermissionKeys.Vendor.BranchManager, "Vendor branch manager role."),
        new("vendor_branch_staff", "Vendor Branch Staff", UserRole.VendorStaff, PanelScope.VendorPanel, PermissionKeys.Vendor.BranchStaff, "Vendor branch staff role."),
        new("vendor_finance_manager", "Vendor Finance Manager", UserRole.VendorStaff, PanelScope.VendorPanel,
        [
            PermissionKeys.Vendor.DashboardView,
            PermissionKeys.Vendor.OrdersView,
            PermissionKeys.Vendor.OrdersExport,
            PermissionKeys.Vendor.FinanceView,
            PermissionKeys.Vendor.FinanceEdit,
            PermissionKeys.Vendor.FinanceExport,
            PermissionKeys.Vendor.FinanceManageSettings
        ], "Vendor finance manager role."),
        new("vendor_support_manager", "Vendor Support Manager", UserRole.VendorStaff, PanelScope.VendorPanel,
        [
            PermissionKeys.Vendor.DashboardView,
            PermissionKeys.Vendor.OrdersView,
            PermissionKeys.Vendor.OrdersEdit,
            PermissionKeys.Vendor.SupportView,
            PermissionKeys.Vendor.SupportEdit,
            PermissionKeys.Vendor.SupportExport
        ], "Vendor support manager role."),
        new("driver_account", "Driver Account", UserRole.Driver, PanelScope.DriverApp, PermissionKeys.Driver.All, "Driver account role."),
        new("customer_account", "Customer Account", UserRole.Customer, PanelScope.CustomerApp, PermissionKeys.Customer.All, "Customer account role.")
    ];

    private static PermissionSeed CreatePermissionSeed(string key)
    {
        var parts = key.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        var domain = parts.Length > 0 ? parts[0] : "admin";
        var action = parts.Length > 1 ? parts[1] : "manage";
        var name = string.Join(' ', domain.Split('_').Append(action).Select(ToDisplayWord));
        var panelScope = domain switch
        {
            _ when domain.StartsWith("vendor_", StringComparison.OrdinalIgnoreCase) => PanelScope.VendorPanel,
            _ when domain.StartsWith("driver_", StringComparison.OrdinalIgnoreCase) => PanelScope.DriverApp,
            _ when domain.StartsWith("customer_", StringComparison.OrdinalIgnoreCase) => PanelScope.CustomerApp,
            _ => PanelScope.SuperAdminPanel
        };
        var isSensitive = action.Equals("approve", StringComparison.OrdinalIgnoreCase) ||
            key.Equals(PermissionKeys.Admin.SystemManageSettings, StringComparison.OrdinalIgnoreCase) ||
            domain.Equals("finances", StringComparison.OrdinalIgnoreCase) ||
            domain.Equals("vendor_finance", StringComparison.OrdinalIgnoreCase) ||
            domain.Equals("wallets", StringComparison.OrdinalIgnoreCase) ||
            domain.Equals("users_access", StringComparison.OrdinalIgnoreCase);

        return new PermissionSeed(
            key,
            name,
            domain,
            action,
            panelScope,
            $"Allows access to {action.Replace('_', ' ')} {domain.Replace('_', ' ')}.",
            isSensitive);
    }

    private static string ToDisplayWord(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return string.Concat(value[..1].ToUpperInvariant(), value[1..].ToLowerInvariant());
    }

    private sealed record PermissionSeed(
        string Key,
        string Name,
        string Domain,
        string Action,
        PanelScope PanelScope,
        string Description,
        bool IsSensitive);

    private sealed record RoleSeed(
        string Code,
        string Name,
        UserRole IdentityRole,
        PanelScope PanelScope,
        IReadOnlyCollection<string> Permissions,
        string Description);
}

public sealed record DevelopmentSeedSummary(
    int Categories,
    int Brands,
    int MasterProducts,
    int Vendors,
    int VendorProducts,
    int Customers,
    int Drivers,
    int Orders,
    int HomeBanners,
    int Coupons,
    int Reviews,
    int Notifications);
