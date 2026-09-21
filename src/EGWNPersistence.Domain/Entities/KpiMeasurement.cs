namespace EGWNPersistence.Domain.Entities;

/// <summary>
/// KPI measurement for a community and timestamp. KPI values are community-scoped
/// (not tied to a single meter). Individual value fields may be null when the source
/// system did not provide a value for that timestamp.
/// </summary>
public class KpiMeasurement
{
    private KpiMeasurement() { }

    public string CommunityId { get; private set; } = string.Empty;
    public DateTimeOffset Timestamp { get; private set; }
    public decimal? Autarky { get; private set; }
    public decimal? OwnConsumption { get; private set; }
    public decimal? Community { get; private set; }
    public decimal? Feed { get; private set; }
    public decimal? RemainingDemand { get; private set; }
    public DateTimeOffset IngestedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static KpiMeasurement Create(
        string communityId,
        DateTimeOffset timestamp,
        decimal? autarky,
        decimal? ownConsumption,
        decimal? community,
        decimal? feed,
        decimal? remainingDemand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communityId);

        var now = DateTimeOffset.UtcNow;
        return new KpiMeasurement
        {
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

    /// <summary>
    /// Overwrites the KPI values with the supplied ones and updates the audit timestamps.
    /// Used for the upsert path when a value for the same (community, timestamp) arrives again.
    /// </summary>
    public void ApplyUpsert(
        decimal? autarky,
        decimal? ownConsumption,
        decimal? community,
        decimal? feed,
        decimal? remainingDemand,
        DateTimeOffset ingestedAt)
    {
        Autarky = autarky;
        OwnConsumption = ownConsumption;
        Community = community;
        Feed = feed;
        RemainingDemand = remainingDemand;
        IngestedAt = ingestedAt;
        UpdatedAt = ingestedAt;
    }

    public static KpiMeasurement Reconstitute(
        string communityId,
        DateTimeOffset timestamp,
        decimal? autarky,
        decimal? ownConsumption,
        decimal? community,
        decimal? feed,
        decimal? remainingDemand,
        DateTimeOffset ingestedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new KpiMeasurement
        {
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
