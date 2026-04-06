using Zadana.Application.Modules.Identity.DTOs;

namespace Zadana.Application.Common.Interfaces;

public interface IJwtTokenService
{
    Task<TokenPairDto> GenerateTokenPairAsync(IdentityAccountSnapshot user, CancellationToken cancellationToken = default);
}
