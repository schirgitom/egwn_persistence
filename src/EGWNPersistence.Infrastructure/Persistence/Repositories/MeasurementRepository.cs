using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EGWNPersistence.Infrastructure.Persistence.Repositories;

public sealed class MeasurementRepository(EGWNPersistenceDbContext dbContext) : IMeasurementRepository
{
    public async Task<IReadOnlyDictionary<DateTimeOffset, MeterMeasurement>> GetExistingAsync(
        long meterPointId,
        string communityId,
        IEnumerable<DateTimeOffset> timestamps,
        CancellationToken cancellationToken = default)
    {
        var tsArray = timestamps.Select(t => t.ToUniversalTime()).ToArray();
        if (tsArray.Length == 0)
            return new Dictionary<DateTimeOffset, MeterMeasurement>();

        var existing = await dbContext.RawMeasurements
            .Where(x => x.MeterPointId == meterPointId
                        && x.CommunityId == communityId
                        && tsArray.Contains(x.Timestamp))
            .ToListAsync(cancellationToken);

        return existing.ToDictionary(x => x.Timestamp.ToUniversalTime(), x => x);
    }

    public async Task BulkInsertAsync(
        IEnumerable<MeterMeasurement> measurements,
        CancellationToken cancellationToken = default)
    {
        var list = measurements.ToList();
        if (list.Count == 0)
            return;

        await dbContext.RawMeasurements.AddRangeAsync(list, cancellationToken);
    }

    public Task UpdateAsync(
        IEnumerable<MeterMeasurement> measurements,
        CancellationToken cancellationToken = default)
    {
        var list = measurements.ToList();
        if (list.Count == 0)
            return Task.CompletedTask;

        dbContext.RawMeasurements.UpdateRange(list);
        return Task.CompletedTask;
    }
}
