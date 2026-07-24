using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zadana.Application.Common.Settings;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Identity.Constants;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;
using Zadana.Infrastructure.Data;
using Zadana.Infrastructure.Settings;

namespace Zadana.Infrastructure.Persistence;

public class ApplicationDbContextInitialiser
{
    private const string DefaultAdminEmail = "admin@system.com";
    private const string DefaultAdminPassword = "Admin@123";
    private const string TestPlatformIban = "SA0380000000608010167519";
    private const string TestPlatformAccountNumber = "608010167519";
    private static readonly string[] TestDriverPayoutIbans =
    [
        "SA5580000000608010164546",
        "SA2880000000608010164547",
        "SA9880000000608010164548"
    ];

    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly BankTransferSettingsOptions _bankTransferSettings;
    private readonly MoyasarSettings _moyasarSettings;

    public ApplicationDbContextInitialiser(
        ApplicationDbContext context,
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<BankTransferSettingsOptions>? bankTransferSettings = null,
        IOptions<MoyasarSettings>? moyasarSettings = null)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _bankTransferSettings = bankTransferSettings?.Value ?? new BankTransferSettingsOptions();
        _moyasarSettings = moyasarSettings?.Value ?? new MoyasarSettings();
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
        await new SaudiGeographySynchronizer(_context).SynchronizeAsync();
        await SeedIdentityRolesAsync();
        await SeedAdminAccessControlAsync();
        await SeedSuperAdminAsync();
        await SeedSuperAdminAccessScopeAsync();
        await SeedUnitsOfMeasureAsync();
        await SeedPlatformBankAccountAsync();
        await SeedPlatformLegalDocumentsAsync();
        await RepairTestingBankAccountsAsync();
    }

    private async Task SeedPlatformLegalDocumentsAsync()
    {
        var existingTypes = await _context.PlatformLegalDocuments
            .Select(item => item.DocumentType)
            .ToListAsync();

        var effectiveAt = new DateTime(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc);
        var added = false;

        foreach (PlatformLegalDocumentType documentType in Enum.GetValues<PlatformLegalDocumentType>())
        {
            if (existingTypes.Contains(documentType))
            {
                continue;
            }

            var contentAr = ReadLegalSeedContent(documentType, "ar");
            var contentEn = ReadLegalSeedContent(documentType, "en");
            _context.PlatformLegalDocuments.Add(new PlatformLegalDocument(
                documentType,
                contentAr,
                contentEn,
                version: "1.0",
                effectiveAtUtc: effectiveAt));
            added = true;
        }

        if (added)
        {
            await _context.SaveChangesAsync();
        }
    }

    private static string ReadLegalSeedContent(PlatformLegalDocumentType documentType, string locale)
    {
        var fileName = $"{documentType}.{locale}.md";
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "SeedData", "Legal", fileName),
            Path.Combine(AppContext.BaseDirectory, "SeedData", "Legal", fileName.ToLowerInvariant()),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "SeedData", "Legal", fileName))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        return string.Empty;
    }

    private async Task SeedPlatformBankAccountAsync()
    {
        if (await _context.PlatformBankAccounts.AnyAsync(account => account.IsActive))
        {
            return;
        }

        var iban = string.IsNullOrWhiteSpace(_bankTransferSettings.Iban)
            ? TestPlatformIban
            : _bankTransferSettings.Iban;
        var accountNumber = string.IsNullOrWhiteSpace(_bankTransferSettings.AccountNumber)
            ? TestPlatformAccountNumber
            : _bankTransferSettings.AccountNumber;
        var bankName = string.IsNullOrWhiteSpace(_bankTransferSettings.BankName)
            ? "Test Bank"
            : _bankTransferSettings.BankName;
        var accountHolderName = string.IsNullOrWhiteSpace(_bankTransferSettings.AccountHolderName)
            ? "Zadana Test Account"
            : _bankTransferSettings.AccountHolderName;

        var enableMoyasarPayouts = _moyasarSettings.Payouts.Enabled &&
            !string.IsNullOrWhiteSpace(_moyasarSettings.Payouts.SourceId);

        _context.PlatformBankAccounts.Add(new PlatformBankAccount(
            bankName,
            accountHolderName,
            iban,
            accountNumber,
            _moyasarSettings.Payouts.DefaultCountry,
            _moyasarSettings.Payouts.DefaultCity,
            isBankTransferEnabled: true,
            isMoyasarPayoutsEnabled: enableMoyasarPayouts,
            moyasarPayoutSourceId: enableMoyasarPayouts ? _moyasarSettings.Payouts.SourceId : null,
            notes: "Seeded testing platform bank account."));

        await _context.SaveChangesAsync();
    }

    private async Task RepairTestingBankAccountsAsync()
    {
        var superAdminId = await _context.Users
            .Where(user => user.Email == DefaultAdminEmail)
            .Select(user => user.Id)
            .FirstOrDefaultAsync();

        await RepairPlatformBankAccountAsync();
        await NormalizeLegacyPerOrderVendorPayoutsAsync();
        await SeedDriverTestingPayoutMethodsAsync();

        await _context.SaveChangesAsync();
    }

    private async Task RepairPlatformBankAccountAsync()
    {
        var account = await _context.PlatformBankAccounts
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (account is null)
        {
            return;
        }

        var iban = IsValidSaudiIban(account.IBAN) ? account.IBAN : TestPlatformIban;
        var accountNumber = string.IsNullOrWhiteSpace(account.AccountNumber)
            ? TestPlatformAccountNumber
            : account.AccountNumber;

        account.Update(
            string.IsNullOrWhiteSpace(account.BankName) ? "Test Bank" : account.BankName,
            string.IsNullOrWhiteSpace(account.AccountHolderName) ? "Zadana Test Account" : account.AccountHolderName,
            iban,
            accountNumber,
            string.IsNullOrWhiteSpace(account.CountryCode) ? _moyasarSettings.Payouts.DefaultCountry : account.CountryCode,
            string.IsNullOrWhiteSpace(account.City) ? _moyasarSettings.Payouts.DefaultCity : account.City,
            account.IsBankTransferEnabled,
            account.IsMoyasarPayoutsEnabled,
            account.MoyasarPayoutSourceId,
            account.Notes ?? "Testing platform bank account.");
    }

    private async Task NormalizeLegacyPerOrderVendorPayoutsAsync()
    {
        var configuredPayoutDays = await _context.SettlementProcessingSettings
            .AsNoTracking()
            .Where(item => item.Id == SettlementProcessingSettings.SingletonId)
            .Select(item => item.PayoutDays)
            .FirstOrDefaultAsync();
        var enabledPayoutDays = string.IsNullOrWhiteSpace(configuredPayoutDays)
            ? PayoutScheduleDayPolicy.DefaultPayoutDays
            : configuredPayoutDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.TryParse<PayoutScheduleDay>(value, true, out var payoutDay)
                    ? (PayoutScheduleDay?)payoutDay
                    : null)
                .Where(value => value.HasValue && PayoutScheduleDayPolicy.IsAllowed(value.Value))
                .Select(value => value!.Value)
                .ToArray();
        var fallbackPayoutDay = PayoutScheduleDayPolicy.ResolveFallback(
            enabledPayoutDays.Count == 0
                ? PayoutScheduleDayPolicy.DefaultPayoutDays
                : enabledPayoutDays);
        var perOrderVendors = await _context.Vendors
            .Where(vendor => vendor.FinancialLifecycleMode == VendorFinancialLifecycleMode.PerOrderDirectPayout)
            .ToListAsync();

        foreach (var vendor in perOrderVendors)
        {
            vendor.UpdateFinanceSettings(
                VendorFinancialLifecycleMode.Weekly,
                "weekly",
                fallbackPayoutDay);
        }
    }

    private async Task SeedDriverTestingPayoutMethodsAsync()
    {
        var drivers = await _context.Drivers
            .Include(driver => driver.User)
            .OrderBy(driver => driver.User.Email)
            .ToListAsync();

        for (var index = 0; index < drivers.Count; index++)
        {
            var driver = drivers[index];
            var existingMethods = await _context.DriverPayoutMethods
                .Where(method => method.DriverId == driver.Id)
                .ToListAsync();

            var primaryBankMethod = existingMethods
                .FirstOrDefault(method => method.MethodType == DriverPayoutMethodType.BankAccount && method.IsPrimary)
                ?? existingMethods.FirstOrDefault(method => method.MethodType == DriverPayoutMethodType.BankAccount);

            var iban = TestDriverPayoutIbans[index % TestDriverPayoutIbans.Length];
            var holderName = string.IsNullOrWhiteSpace(driver.User.FullName)
                ? driver.User.Email ?? "Zadana Test Driver"
                : driver.User.FullName;

            if (primaryBankMethod is null)
            {
                foreach (var method in existingMethods.Where(method => method.IsPrimary))
                {
                    method.UnsetPrimary();
                }

                _context.DriverPayoutMethods.Add(new DriverPayoutMethod(
                    driver.Id,
                    DriverPayoutMethodType.BankAccount,
                    holderName,
                    iban,
                    "Test Bank",
                    isPrimary: true));
                continue;
            }

            if (!IsValidSaudiIban(primaryBankMethod.AccountIdentifier))
            {
                primaryBankMethod.UpdateDetails(
                    DriverPayoutMethodType.BankAccount,
                    holderName,
                    iban,
                    string.IsNullOrWhiteSpace(primaryBankMethod.ProviderName) ? "Test Bank" : primaryBankMethod.ProviderName);
            }

            foreach (var method in existingMethods.Where(method => method.Id != primaryBankMethod.Id && method.IsPrimary))
            {
                method.UnsetPrimary();
            }

            primaryBankMethod.SetPrimary();
        }
    }

    private static bool IsValidSaudiIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return false;
        }

        var clean = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return clean.Length == 24
            && clean.StartsWith("SA", StringComparison.OrdinalIgnoreCase)
            && clean.Skip(2).All(char.IsDigit)
            && PassesIbanChecksum(clean);
    }

    private static bool PassesIbanChecksum(string iban)
    {
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;

        foreach (var ch in rearranged)
        {
            if (char.IsDigit(ch))
            {
                remainder = (remainder * 10 + (ch - '0')) % 97;
                continue;
            }

            if (char.IsLetter(ch))
            {
                var value = char.ToUpperInvariant(ch) - 'A' + 10;
                remainder = (remainder * 10 + (value / 10)) % 97;
                remainder = (remainder * 10 + (value % 10)) % 97;
            }
        }

        return remainder == 1;
    }

    private async Task SeedUnitsOfMeasureAsync()
    {
        var seeds = BuildUnitSeeds();
        var names = seeds.Select(seed => seed.NameEn).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingUnits = await _context.UnitsOfMeasure.ToListAsync();
        var existingByNameEn = existingUnits
            .GroupBy(unit => unit.NameEn, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seeds)
        {
            if (existingByNameEn.TryGetValue(seed.NameEn, out var existing))
            {
                existing.Update(seed.NameAr, seed.NameEn, seed.Symbol, ResolveUnitKind(seed.NameEn));
                existing.Activate();
                continue;
            }

            _context.UnitsOfMeasure.Add(new UnitOfMeasure(seed.NameAr, seed.NameEn, seed.Symbol, ResolveUnitKind(seed.NameEn)));
        }

        foreach (var obsoleteUnit in existingUnits.Where(unit => !names.Contains(unit.NameEn)))
        {
            obsoleteUnit.Deactivate();
        }

        await _context.SaveChangesAsync();
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

    private static IReadOnlyList<UnitSeed> BuildUnitSeeds() =>
    [
        new("قطعة", "Piece", "pc"),
        new("عبوة", "Pack", "pack"),
        new("علبة", "Box", "box"),
        new("كرتونة", "Carton", "ctn"),
        new("زجاجة", "Bottle", "btl"),
        new("برطمان", "Jar", "jar"),
        new("علبة معدنية", "Can", "can"),
        new("كيس تغليف", "Pouch", "pouch"),
        new("كيس صغير", "Sachet", "sachet"),
        new("كيس", "Bag", "bag"),
        new("صينية", "Tray", "tray"),
        new("أنبوب", "Tube", "tube"),
        new("كبسولة", "Capsule", "cap"),
        new("قرص", "Tablet", "tab"),
        new("قارورة صغيرة", "Vial", "vial"),
        new("أمبول", "Ampoule", "amp"),
        new("كيلوجرام", "Kilogram", "kg"),
        new("جرام", "Gram", "g"),
        new("ملليجرام", "Milligram", "mg"),
        new("لتر", "Liter", "L"),
        new("ملليلتر", "Milliliter", "mL")
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

    private static UnitKind ResolveUnitKind(string unitNameEn)
    {
        return unitNameEn switch
        {
            "Piece" or "Pack" or "Box" or "Carton" or "Bottle" or "Jar" or "Can" or "Pouch" or "Sachet" or "Bag" or "Tray" or "Tube" or "Capsule" or "Tablet" or "Vial" or "Ampoule"
                => UnitKind.Packaging,
            _ => UnitKind.Measurement
        };
    }

    private sealed record PermissionSeed(
        string Key,
        string Name,
        string Domain,
        string Action,
        PanelScope PanelScope,
        string Description,
        bool IsSensitive);

    private sealed record UnitSeed(
        string NameAr,
        string NameEn,
        string Symbol);

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
    int Notifications);
