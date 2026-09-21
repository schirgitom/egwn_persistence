using System.Text.Json.Serialization;

namespace EGWNPersistence.Application.DTOs;

/// <summary>
/// RabbitMQ payload for community-scoped KPI values. Each entry carries a timestamp
/// and a nested <see cref="KpiValuesDto"/> whose individual fields may be null.
/// </summary>
public sealed class KpiMessageDto
{
    [JsonPropertyName("communityId")]
    public string CommunityId { get; init; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [JsonPropertyName("values")]
    public List<KpiValueEntryDto> Values { get; init; } = [];
}

public sealed class KpiValueEntryDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("values")]
    public KpiValuesDto Values { get; init; } = new();
}

public sealed class KpiValuesDto
{
    [JsonPropertyName("autarky")]
    public decimal? Autarky { get; init; }

    [JsonPropertyName("ownConsumption")]
    public decimal? OwnConsumption { get; init; }

    [JsonPropertyName("community")]
    public decimal? Community { get; init; }

    [JsonPropertyName("feed")]
    public decimal? Feed { get; init; }

    [JsonPropertyName("remainingDemand")]
    public decimal? RemainingDemand { get; init; }
}
