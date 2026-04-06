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
}
