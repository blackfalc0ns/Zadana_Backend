using Microsoft.EntityFrameworkCore;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Infrastructure.Persistence;

namespace Zadana.Infrastructure.Modules.Delivery.Repositories;

public class DriverRepository : IDriverRepository
{
    private readonly ApplicationDbContext _dbContext;

    public DriverRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(Driver driver) => _dbContext.Drivers.Add(driver);

    public async Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Drivers
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Drivers
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
    }
}
