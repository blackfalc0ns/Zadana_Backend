using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Modules.Identity.Interfaces;

public interface IAccessControlService
{
    Task<EffectiveAccessDto> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken = default);
}
