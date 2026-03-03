using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Common.Interfaces;

public interface IJwtTokenService
{
    Task<TokenPairDto> GenerateTokenPairAsync(User user, CancellationToken cancellationToken = default);
}
