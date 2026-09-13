using MediatR;

namespace EGWNPersistence.Application.Commands;

/// <summary>
/// Command to ingest a batch of meter measurements from a RabbitMQ message.
/// </summary>
public sealed record IngestMeasurementsCommand(
    string MeterPointNumber,
    IReadOnlyList<MeasurementEntry> Measurements) : IRequest<IngestMeasurementsResult>;

public sealed record MeasurementEntry(
    DateTimeOffset Timestamp,
    decimal TotalConsumption,
    decimal GridShare,
    decimal CommunityShare,
    decimal GridSharePercent,
    decimal CommunitySharePercent,
    string Source);

public sealed record IngestMeasurementsResult(
    long MeterPointId,
    int TotalReceived,
    int Inserted,
    int Skipped);
