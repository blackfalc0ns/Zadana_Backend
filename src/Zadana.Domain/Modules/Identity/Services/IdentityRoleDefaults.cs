using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Domain.Modules.Identity.Services;

public static class IdentityRoleDefaults
{
    public static string ResolvePreferredRoleCode(UserRole role) => role switch
    {
        UserRole.SuperAdmin => "super_admin_all",
        UserRole.Admin => "admin_operations",
        UserRole.Vendor => "vendor_owner",
        UserRole.VendorStaff => "vendor_company_manager",
        UserRole.Driver => "driver_account",
        UserRole.Customer => "customer_account",
        _ => "admin_operations"
    };
}
