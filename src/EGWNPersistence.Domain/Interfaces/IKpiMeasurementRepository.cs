using EGWNPersistence.Domain.Entities;

namespace EGWNPersistence.Domain.Interfaces;

public interface IKpiMeasurementRepository
{
    /// <summary>
    /// Returns the KPI rows that already exist for the given community and timestamps.
    /// Used to decide between insert and update paths.
    /// </summary>
    Task<IReadOnlyDictionary<DateTimeOffset, KpiMeasurement>> GetExistingAsync(
        string communityId,
        IEnumerable<DateTimeOffset> timestamps,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-inserts new KPI rows.
    /// </summary>
    Task BulkInsertAsync(IEnumerable<KpiMeasurement> measurements, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks existing KPI rows as updated so EF Core writes them on SaveChanges.
    /// </summary>
    Task UpdateAsync(IEnumerable<KpiMeasurement> measurements, CancellationToken cancellationToken = default);
}
