using System.Text.Json.Serialization;

namespace EGWNPersistence.Application.DTOs;

public sealed class KpiMessageDto
{
    [JsonPropertyName("meterId")]
    public string MeterId { get; init; } = string.Empty;

    [JsonPropertyName("communityId")]
    public string CommunityId { get; init; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; init; }

    [JsonPropertyName("values")]
    public List<KpiValueDto> Values { get; init; } = [];
}

public sealed class KpiValueDto
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("autarky")]
    public decimal Autarky { get; init; }

    [JsonPropertyName("ownConsumption")]
    public decimal OwnConsumption { get; init; }

    [JsonPropertyName("community")]
    public decimal Community { get; init; }

    [JsonPropertyName("feed")]
    public decimal Feed { get; init; }

    [JsonPropertyName("remainingDemand")]
    public decimal RemainingDemand { get; init; }
}
