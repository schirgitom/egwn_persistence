using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EGWNPersistence.Infrastructure.Persistence.Repositories;

public sealed class KpiMeasurementRepository(EGWNPersistenceDbContext dbContext) : IKpiMeasurementRepository
{
    public async Task<IReadOnlyDictionary<DateTimeOffset, KpiMeasurement>> GetExistingAsync(
        string communityId,
        IEnumerable<DateTimeOffset> timestamps,
        CancellationToken cancellationToken = default)
    {
        var tsArray = timestamps.Select(t => t.ToUniversalTime()).ToArray();
        if (tsArray.Length == 0)
            return new Dictionary<DateTimeOffset, KpiMeasurement>();

        var existing = await dbContext.KpiMeasurements
            .Where(x => x.CommunityId == communityId && tsArray.Contains(x.Timestamp))
            .ToListAsync(cancellationToken);

        return existing.ToDictionary(x => x.Timestamp.ToUniversalTime(), x => x);
    }

    public async Task BulkInsertAsync(
        IEnumerable<KpiMeasurement> measurements,
        CancellationToken cancellationToken = default)
    {
        var list = measurements.ToList();
        if (list.Count == 0)
            return;

        await dbContext.KpiMeasurements.AddRangeAsync(list, cancellationToken);
    }

    public Task UpdateAsync(
        IEnumerable<KpiMeasurement> measurements,
        CancellationToken cancellationToken = default)
    {
        var list = measurements.ToList();
        if (list.Count == 0)
            return Task.CompletedTask;

        dbContext.KpiMeasurements.UpdateRange(list);
        return Task.CompletedTask;
    }
}
