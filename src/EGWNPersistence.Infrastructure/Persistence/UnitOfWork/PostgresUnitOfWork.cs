using EGWNPersistence.Domain.Interfaces;
using EGWNPersistence.Infrastructure.Persistence.Repositories;
using Npgsql;

namespace EGWNPersistence.Infrastructure.Persistence.UnitOfWork;

public sealed class PostgresUnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _connection;
    private NpgsqlTransaction? _transaction;
    private bool _disposed;

    public IMeterPointRepository MeterPoints { get; private set; }
    public IMeasurementRepository Measurements { get; private set; }

    public PostgresUnitOfWork(NpgsqlDataSource dataSource)
    {
        _connection = dataSource.CreateConnection();
        // Repositories are initialized without a transaction; they'll use the active one after BeginAsync
        MeterPoints = new MeterPointRepository(_connection, null);
        Measurements = new MeasurementRepository(_connection, null);
    }

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(cancellationToken);

        _transaction = await _connection.BeginTransactionAsync(cancellationToken);

        // Re-create repositories with the active transaction
        MeterPoints = new MeterPointRepository(_connection, _transaction);
        Measurements = new MeasurementRepository(_connection, _transaction);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Transaction has not been started.");
        await _transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            await _transaction.RollbackAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        await _connection.DisposeAsync();
    }
}
