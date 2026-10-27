using EGWNPersistence.Domain.Aggregates;

namespace EGWNPersistence.Domain.Interfaces;

public interface IMeterPointRepository
{
    Task<MeterPoint?> GetByMeterPointNumberAsync(string meterPointNumber, CancellationToken cancellationToken = default);
    Task AddAsync(MeterPoint meterPoint, CancellationToken cancellationToken = default);
    Task EnsureCommunityMembershipAsync(long meterPointId, string communityId, CancellationToken cancellationToken = default);
}
