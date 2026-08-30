namespace EGWNPersistence.Domain.Entities;

/// <summary>
/// Represents a single hourly energy measurement for a meter point.
/// This entity is append-only — never updated after creation.
/// </summary>
public class MeterMeasurement
{
    private MeterMeasurement() { }

    public Guid Id { get; private set; }
    public Guid MeterPointId { get; private set; }

    /// <summary>Start of the measurement period (always UTC, hourly granularity).</summary>
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>Total energy consumption in kWh.</summary>
    public decimal TotalConsumption { get; private set; }

    /// <summary>Grid share of consumption in kWh.</summary>
    public decimal GridShare { get; private set; }

    /// <summary>Community (energy community) share of consumption in kWh.</summary>
    public decimal CommunityShare { get; private set; }

    /// <summary>Grid share as fraction of total (0–1).</summary>
    public decimal GridSharePercent { get; private set; }

    /// <summary>Community share as fraction of total (0–1).</summary>
    public decimal CommunitySharePercent { get; private set; }

    /// <summary>Data source identifier (e.g. "EDA").</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>Server-side timestamp when this record was ingested.</summary>
    public DateTimeOffset IngestedAt { get; private set; }

    /// <summary>Server-side creation timestamp (immutable).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    public static MeterMeasurement Create(
        Guid meterPointId,
        DateTimeOffset timestamp,
        decimal totalConsumption,
        decimal gridShare,
        decimal communityShare,
        decimal gridSharePercent,
        decimal communitySharePercent,
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var now = DateTimeOffset.UtcNow;
        return new MeterMeasurement
        {
            Id = Guid.NewGuid(),
            MeterPointId = meterPointId,
            Timestamp = timestamp.ToUniversalTime(),
            TotalConsumption = totalConsumption,
            GridShare = gridShare,
            CommunityShare = communityShare,
            GridSharePercent = gridSharePercent,
            CommunitySharePercent = communitySharePercent,
            Source = source.Trim(),
            IngestedAt = now,
            CreatedAt = now
        };
    }

    /// <summary>Reconstitutes a measurement from the database (for repository use only).</summary>
    public static MeterMeasurement Reconstitute(
        Guid id,
        Guid meterPointId,
        DateTimeOffset timestamp,
        decimal totalConsumption,
        decimal gridShare,
        decimal communityShare,
        decimal gridSharePercent,
        decimal communitySharePercent,
        string source,
        DateTimeOffset ingestedAt,
        DateTimeOffset createdAt)
    {
        return new MeterMeasurement
        {
            Id = id,
            MeterPointId = meterPointId,
            Timestamp = timestamp,
            TotalConsumption = totalConsumption,
            GridShare = gridShare,
            CommunityShare = communityShare,
            GridSharePercent = gridSharePercent,
            CommunitySharePercent = communitySharePercent,
            Source = source,
            IngestedAt = ingestedAt,
            CreatedAt = createdAt
        };
    }
}
