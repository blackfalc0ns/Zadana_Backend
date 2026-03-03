using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Domain.Modules.Identity.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    void Add(User user);
    void Update(User user);
}
