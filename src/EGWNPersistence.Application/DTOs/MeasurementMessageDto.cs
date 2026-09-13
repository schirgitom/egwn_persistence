using System.Text.Json.Serialization;

namespace EGWNPersistence.Application.DTOs;

/// <summary>
/// Represents the RabbitMQ measurement payload from the EDA meter reading feed.
/// </summary>
public sealed class MeterReadingMessageDto
{
    [JsonPropertyName("meterPointNumber")]
    public string MeterPointNumber { get; init; } = string.Empty;

    [JsonPropertyName("communityId")]
    public string CommunityId { get; init; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [JsonPropertyName("readings")]
    public List<MeterReadingEntryDto> Readings { get; init; } = [];
}

public sealed class MeterReadingEntryDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("totalConsumptionValue")]
    public decimal TotalConsumptionValue { get; init; }

    [JsonPropertyName("gridShareValue")]
    public decimal GridShareValue { get; init; }

    [JsonPropertyName("communityShareValue")]
    public decimal CommunityShareValue { get; init; }
}
