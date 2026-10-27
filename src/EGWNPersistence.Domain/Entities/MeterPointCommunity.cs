namespace EGWNPersistence.Domain.Entities;

public sealed class MeterPointCommunity
{
    private MeterPointCommunity() { }

    public long MeterPointId { get; private set; }
    public string CommunityId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
