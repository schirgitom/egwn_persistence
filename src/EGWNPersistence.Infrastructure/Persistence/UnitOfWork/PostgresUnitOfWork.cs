using EGWNPersistence.Domain.Interfaces;
using EGWNPersistence.Infrastructure.Persistence;
using EGWNPersistence.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EGWNPersistence.Infrastructure.Persistence.UnitOfWork;

public sealed class PostgresUnitOfWork(EGWNPersistenceDbContext dbContext) : IUnitOfWork
{
    private readonly EGWNPersistenceDbContext _dbContext = dbContext;

    public IMeterPointRepository MeterPoints { get; } = new MeterPointRepository(dbContext);
    public IMeasurementRepository Measurements { get; } = new MeasurementRepository(dbContext);
    public IKpiMeasurementRepository KpiMeasurements { get; } = new KpiMeasurementRepository(dbContext);

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is null)
        {
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_dbContext.Database.CurrentTransaction is not null)
        {
            await _dbContext.Database.CurrentTransaction.CommitAsync(cancellationToken);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            await _dbContext.Database.CurrentTransaction.RollbackAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            await _dbContext.Database.CurrentTransaction.DisposeAsync();
        }

        await _dbContext.DisposeAsync();
    }
}
