namespace EGWNPersistence.Domain.Entities;

/// <summary>
/// KPI measurement for a meter point and community.
/// </summary>
public class KpiMeasurement
{
    private KpiMeasurement() { }

    public long MeterPointId { get; private set; }
    public string CommunityId { get; private set; } = string.Empty;
    public DateTimeOffset Timestamp { get; private set; }
    public decimal Autarky { get; private set; }
    public decimal OwnConsumption { get; private set; }
    public decimal Community { get; private set; }
    public decimal Feed { get; private set; }
    public decimal RemainingDemand { get; private set; }
    public DateTimeOffset IngestedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static KpiMeasurement Create(
        long meterPointId,
        string communityId,
        DateTimeOffset timestamp,
        decimal autarky,
        decimal ownConsumption,
        decimal community,
        decimal feed,
        decimal remainingDemand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityId);

        var now = DateTimeOffset.UtcNow;
        return new KpiMeasurement
        {
            MeterPointId = meterPointId,
            CommunityId = communityId.Trim(),
            Timestamp = timestamp.ToUniversalTime(),
            Autarky = autarky,
            OwnConsumption = ownConsumption,
            Community = community,
            Feed = feed,
            RemainingDemand = remainingDemand,
            IngestedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static KpiMeasurement Reconstitute(
        long meterPointId,
        string communityId,
        DateTimeOffset timestamp,
        decimal autarky,
        decimal ownConsumption,
        decimal community,
        decimal feed,
        decimal remainingDemand,
        DateTimeOffset ingestedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new KpiMeasurement
        {
            MeterPointId = meterPointId,
            CommunityId = communityId,
            Timestamp = timestamp,
            Autarky = autarky,
            OwnConsumption = ownConsumption,
            Community = community,
            Feed = feed,
            RemainingDemand = remainingDemand,
            IngestedAt = ingestedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
