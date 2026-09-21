using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EGWNPersistence.Application.Commands;

public sealed class IngestKpisHandler(
    IUnitOfWork unitOfWork,
    ILogger<IngestKpisHandler> logger)
    : IRequestHandler<IngestKpisCommand, IngestKpisResult>
{
    public async Task<IngestKpisResult> Handle(
        IngestKpisCommand request,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);

        try
        {
            var incomingTimestamps = request.Entries
                .Select(e => e.Timestamp.ToUniversalTime())
                .Distinct()
                .ToList();

            var existing = await unitOfWork.KpiMeasurements
                .GetExistingAsync(request.CommunityId, incomingTimestamps, cancellationToken);

            var toInsert = new List<KpiMeasurement>();
            var toUpdate = new List<KpiMeasurement>();
            var utcNow = DateTimeOffset.UtcNow;

            foreach (var entry in request.Entries)
            {
                var timestampUtc = entry.Timestamp.ToUniversalTime();

                if (existing.TryGetValue(timestampUtc, out var current))
                {
                    current.ApplyUpsert(
                        entry.Autarky,
                        entry.OwnConsumption,
                        entry.Community,
                        entry.Feed,
                        entry.RemainingDemand,
                        utcNow);
                    toUpdate.Add(current);
                    continue;
                }

                toInsert.Add(KpiMeasurement.Create(
                    request.CommunityId,
                    entry.Timestamp,
                    entry.Autarky,
                    entry.OwnConsumption,
                    entry.Community,
                    entry.Feed,
                    entry.RemainingDemand));
            }

            if (toInsert.Count > 0)
            {
                await unitOfWork.KpiMeasurements.BulkInsertAsync(toInsert, cancellationToken);
            }

            if (toUpdate.Count > 0)
            {
                await unitOfWork.KpiMeasurements.UpdateAsync(toUpdate, cancellationToken);
            }

            await unitOfWork.CommitAsync(cancellationToken);

            var result = new IngestKpisResult(
                request.CommunityId,
                request.Entries.Count,
                toInsert.Count,
                toUpdate.Count);

            logger.LogInformation(
                "Ingested KPIs for community {CommunityId}: {Inserted} inserted, {Updated} updated",
                request.CommunityId, result.Inserted, result.Updated);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest KPIs for community {CommunityId}", request.CommunityId);
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
