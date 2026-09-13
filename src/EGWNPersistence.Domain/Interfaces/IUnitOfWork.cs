namespace EGWNPersistence.Domain.Interfaces;

/// <summary>
/// Unit of Work abstraction. Wraps a single database connection and transaction.
/// Repositories participating in the same UoW share the same connection/transaction.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IMeterPointRepository MeterPoints { get; }
    IMeasurementRepository Measurements { get; }

    Task BeginAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
