using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IRegistrationRoleMaterializer
{
    Task MaterializeAsync(
        IdentityAccountSnapshot account,
        UserRole role,
        string payloadJson,
        CancellationToken cancellationToken = default);
}
