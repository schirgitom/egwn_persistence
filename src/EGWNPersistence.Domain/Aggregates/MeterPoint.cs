namespace EGWNPersistence.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a metering point (Zählpunkt) in the energy community.
/// </summary>
public class MeterPoint
{
    private MeterPoint() { }

    public long Id { get; private set; }
    public string MeterPointNumber { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static MeterPoint Create(string meterPointNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meterPointNumber);

        return new MeterPoint
        {
            Id = 0,
            MeterPointNumber = meterPointNumber.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Reconstitutes a MeterPoint from the database (for repository use only).</summary>
    public static MeterPoint Reconstitute(long id, string meterPointNumber, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        return new MeterPoint
        {
            Id = id,
            MeterPointNumber = meterPointNumber,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
