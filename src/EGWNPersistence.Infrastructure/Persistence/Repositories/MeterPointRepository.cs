using EGWNPersistence.Domain.Aggregates;
using EGWNPersistence.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EGWNPersistence.Infrastructure.Persistence.Repositories;

public sealed class MeterPointRepository(EGWNPersistenceDbContext dbContext) : IMeterPointRepository
{
    public async Task<MeterPoint?> GetByMeterPointNumberAsync(
        string meterPointNumber,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MeterPoints
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.MeterPointNumber == meterPointNumber, cancellationToken);
    }

    public async Task AddAsync(MeterPoint meterPoint, CancellationToken cancellationToken = default)
    {
        await dbContext.MeterPoints.AddAsync(meterPoint, cancellationToken);
    }
}
