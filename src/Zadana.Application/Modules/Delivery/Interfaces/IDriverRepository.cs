using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Application.Modules.Delivery.Interfaces;

public interface IDriverRepository
{
    void Add(Driver driver);
    Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
