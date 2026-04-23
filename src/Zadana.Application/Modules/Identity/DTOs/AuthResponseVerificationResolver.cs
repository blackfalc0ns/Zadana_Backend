using Zadana.Application.Modules.Delivery.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.DTOs;

public static class AuthResponseVerificationResolver
{
    public static bool Resolve(UserRole role, DriverOperationalStatusDto? driverStatus) =>
        role != UserRole.Driver || driverStatus?.IsOperational == true;
}
