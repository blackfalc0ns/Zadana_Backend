using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Common.Extensions;

public static class CurrentUserServiceExtensions
{
    public static bool HasRole(this ICurrentUserService currentUserService, params UserRole[] roles)
    {
        if (currentUserService is null || string.IsNullOrWhiteSpace(currentUserService.Role))
        {
            return false;
        }

        return Enum.TryParse<UserRole>(currentUserService.Role, true, out var parsedRole)
            && roles.Contains(parsedRole);
    }
}
