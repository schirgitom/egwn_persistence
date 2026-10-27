namespace EGWNPersistence.Domain.Entities;

/// <summary>
/// Represents a single hourly energy measurement for a meter point.
/// Raw values are mutable during the correction window (three weeks after the timestamp),
/// after which they are treated as fixed historical data.
/// </summary>
public class MeterMeasurement
{
    public static readonly TimeSpan CorrectionWindow = TimeSpan.FromDays(21);

    private MeterMeasurement() { }

    public long MeterPointId { get; private set; }
    public string CommunityId { get; private set; } = string.Empty;

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

    /// <summary>Server-side creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Last update timestamp. Allows corrections during the configurable correction window.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool CanBeCorrected(DateTimeOffset now, TimeSpan? correctionWindow = null)
    {
        var currentUtc = now.ToUniversalTime();
        var timestampUtc = Timestamp.ToUniversalTime();
        var effectiveWindow = correctionWindow ?? CorrectionWindow;
        return currentUtc >= timestampUtc && currentUtc - timestampUtc <= effectiveWindow;
    }

    public void ApplyCorrection(
        decimal totalConsumption,
        decimal gridShare,
        decimal communityShare,
        decimal gridSharePercent,
        decimal communitySharePercent,
        string source,
        DateTimeOffset ingestedAt,
        DateTimeOffset now,
        TimeSpan? correctionWindow = null)
    {
        if (!CanBeCorrected(now, correctionWindow))
        {
            throw new InvalidOperationException("This measurement is outside the correction window and cannot be updated.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        TotalConsumption = totalConsumption;
        GridShare = gridShare;
        CommunityShare = communityShare;
        GridSharePercent = gridSharePercent;
        CommunitySharePercent = communitySharePercent;
        Source = source.Trim();
        IngestedAt = ingestedAt.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }

    public static MeterMeasurement Create(
        long meterPointId,
        string communityId,
        DateTimeOffset timestamp,
        decimal totalConsumption,
        decimal gridShare,
        decimal communityShare,
        decimal gridSharePercent,
        decimal communitySharePercent,
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(communityId);

        var now = DateTimeOffset.UtcNow;
        return new MeterMeasurement
        {
            MeterPointId = meterPointId,
            CommunityId = communityId.Trim(),
            Timestamp = timestamp.ToUniversalTime(),
            TotalConsumption = totalConsumption,
            GridShare = gridShare,
            CommunityShare = communityShare,
            GridSharePercent = gridSharePercent,
            CommunitySharePercent = communitySharePercent,
            Source = source.Trim(),
            IngestedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Reconstitutes a measurement from the database (for repository use only).</summary>
    public static MeterMeasurement Reconstitute(
        long meterPointId,
        string communityId,
        DateTimeOffset timestamp,
        decimal totalConsumption,
        decimal gridShare,
        decimal communityShare,
        decimal gridSharePercent,
        decimal communitySharePercent,
        string source,
        DateTimeOffset ingestedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new MeterMeasurement
        {
            MeterPointId = meterPointId,
            CommunityId = communityId,
            Timestamp = timestamp,
            TotalConsumption = totalConsumption,
            GridShare = gridShare,
            CommunityShare = communityShare,
            GridSharePercent = gridSharePercent,
            CommunitySharePercent = communitySharePercent,
            Source = source,
            IngestedAt = ingestedAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
