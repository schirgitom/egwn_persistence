namespace EGWNPersistence.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a metering point (Zählpunkt) in the energy community.
/// </summary>
public class MeterPoint
{
    private MeterPoint() { }

    public long Id { get; private set; }
    public string MeterPointNumber { get; private set; } = string.Empty;
    public string? EnergyDirection { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static MeterPoint Create(string meterPointNumber, string? energyDirection = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meterPointNumber);

        var meterPoint = new MeterPoint
        {
            Id = 0,
            MeterPointNumber = meterPointNumber.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        meterPoint.UpdateEnergyDirection(energyDirection);
        return meterPoint;
    }

    public bool UpdateEnergyDirection(string? energyDirection)
    {
        if (energyDirection is null)
            return false;

        var normalized = energyDirection.Trim().ToLowerInvariant();
        if (normalized is not ("consumption" or "generation"))
            throw new ArgumentException("EnergyDirection must be 'consumption' or 'generation'.", nameof(energyDirection));

        if (EnergyDirection == normalized)
            return false;

        EnergyDirection = normalized;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>Reconstitutes a MeterPoint from the database (for repository use only).</summary>
    public static MeterPoint Reconstitute(long id, string meterPointNumber, DateTimeOffset createdAt, DateTimeOffset updatedAt, string? energyDirection = null)
    {
        return new MeterPoint
        {
            Id = id,
            MeterPointNumber = meterPointNumber,
            EnergyDirection = energyDirection,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
