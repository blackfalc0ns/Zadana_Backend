using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
