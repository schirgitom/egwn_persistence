using MediatR;

namespace EGWNPersistence.Application.Commands;

/// <summary>
/// Command to ingest a batch of community-scoped KPI values from a RabbitMQ message.
/// </summary>
public sealed record IngestKpisCommand(
    string CommunityId,
    IReadOnlyList<KpiEntry> Entries) : IRequest<IngestKpisResult>;

public sealed record KpiEntry(
    DateTimeOffset Timestamp,
    decimal? Autarky,
    decimal? OwnConsumption,
    decimal? Community,
    decimal? Feed,
    decimal? RemainingDemand);

public sealed record IngestKpisResult(
    string CommunityId,
    int TotalReceived,
    int Inserted,
    int Updated);
