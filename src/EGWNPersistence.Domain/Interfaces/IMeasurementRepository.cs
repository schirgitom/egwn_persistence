using EGWNPersistence.Domain.Entities;

namespace EGWNPersistence.Domain.Interfaces;

public interface IMeasurementRepository
{
    /// <summary>
    /// Returns the measurements that already exist for the given meter point and timestamps.
    /// Used for idempotency and correction-window checks.
    /// </summary>
    Task<IReadOnlyDictionary<DateTimeOffset, MeterMeasurement>> GetExistingAsync(
        long meterPointId,
        string communityId,
        IEnumerable<DateTimeOffset> timestamps,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-inserts new measurements.
    /// </summary>
    Task BulkInsertAsync(IEnumerable<MeterMeasurement> measurements, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates existing measurement rows when a new value arrives within the correction window.
    /// </summary>
    Task UpdateAsync(IEnumerable<MeterMeasurement> measurements, CancellationToken cancellationToken = default);
}
