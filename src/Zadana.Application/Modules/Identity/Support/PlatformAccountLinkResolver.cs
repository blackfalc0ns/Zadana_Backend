using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Application.Modules.Identity.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Support;

public static class PlatformAccountLinkResolver
{
    /// <summary>
    /// null = create a new account.
    /// Guid.Empty = occupying role already present, or email and phone belong to different users.
    /// Other Guid = add the registering role onto that account.
    /// </summary>
    public static async Task<Guid?> ResolveAsync(
        IIdentityAccountService identityAccountService,
        string email,
        string? phone,
        UserRole registeringAs,
        CancellationToken cancellationToken)
    {
        var occupyingRoles = PlatformRoleMembership.OccupyingRoles(registeringAs);
        IdentityAccountSnapshot? emailOwner = null;
        var byEmail = await identityAccountService.FindByIdentifierAsync(email, cancellationToken);
        if (byEmail is not null &&
            !string.IsNullOrWhiteSpace(byEmail.Email) &&
            string.Equals(byEmail.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            emailOwner = byEmail;
        }

        IdentityAccountSnapshot? phoneOwner = null;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var byPhone = await identityAccountService.FindByIdentifierAsync(phone, cancellationToken);
            if (byPhone is not null &&
                !string.IsNullOrWhiteSpace(byPhone.PhoneNumber) &&
                string.Equals(byPhone.PhoneNumber, phone, StringComparison.Ordinal))
            {
                phoneOwner = byPhone;
            }
        }

        if (emailOwner is not null && phoneOwner is not null && emailOwner.Id != phoneOwner.Id)
        {
            return Guid.Empty;
        }

        var candidate = emailOwner ?? phoneOwner;
        if (candidate is null)
        {
            return null;
        }

        if (!PlatformRoleMembership.IsSelfServePlatformRole(registeringAs) ||
            PlatformRoleMembership.HasAnyRole(candidate, occupyingRoles))
        {
            return Guid.Empty;
        }

        return candidate.Id;
    }
}
