using EGWNPersistence.Domain.Entities;

namespace EGWNPersistence.Domain.Interfaces;

public interface IMeasurementRepository
{
    /// <summary>
    /// Returns the set of timestamps (UTC) that already exist for the given meter point.
    /// Used for idempotency filtering before bulk insert.
    /// </summary>
    Task<IReadOnlySet<DateTimeOffset>> GetExistingTimestampsAsync(
        Guid meterPointId,
        IEnumerable<DateTimeOffset> timestamps,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-inserts measurements. Silently skips duplicates (ON CONFLICT DO NOTHING).
    /// </summary>
    Task BulkInsertAsync(IEnumerable<MeterMeasurement> measurements, CancellationToken cancellationToken = default);
}
