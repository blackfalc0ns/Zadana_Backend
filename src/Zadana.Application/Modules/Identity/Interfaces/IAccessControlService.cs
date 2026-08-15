using Zadana.Application.Modules.Identity.DTOs;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IAccessControlService
{
    Task<EffectiveAccessDto> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<EffectiveAccessDto> GetEffectiveAccessAsync(Guid userId, UserRole sessionRole, CancellationToken cancellationToken = default);
}
