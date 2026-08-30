using System.Text.Json.Serialization;

namespace EGWNPersistence.Application.DTOs;

/// <summary>
/// Represents the RabbitMQ message payload for EDA meter measurements.
/// </summary>
public sealed class MeasurementMessageDto
{
    [JsonPropertyName("MeterPointNumber")]
    public string MeterPointNumber { get; init; } = string.Empty;

    [JsonPropertyName("Measurements")]
    public List<MeasurementEntryDto> Measurements { get; init; } = [];
}

public sealed class MeasurementEntryDto
{
    [JsonPropertyName("Timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("TotalConsumption")]
    public decimal TotalConsumption { get; init; }

    [JsonPropertyName("GridShare")]
    public decimal GridShare { get; init; }

    [JsonPropertyName("CommunityShare")]
    public decimal CommunityShare { get; init; }

    [JsonPropertyName("GridSharePercent")]
    public decimal GridSharePercent { get; init; }

    [JsonPropertyName("CommunitySharePercent")]
    public decimal CommunitySharePercent { get; init; }

    [JsonPropertyName("Source")]
    public string Source { get; init; } = string.Empty;
}
