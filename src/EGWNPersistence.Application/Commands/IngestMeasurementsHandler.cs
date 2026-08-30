using EGWNPersistence.Domain.Aggregates;
using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EGWNPersistence.Application.Commands;

public sealed class IngestMeasurementsHandler(
    IUnitOfWork unitOfWork,
    ILogger<IngestMeasurementsHandler> logger)
    : IRequestHandler<IngestMeasurementsCommand, IngestMeasurementsResult>
{
    public async Task<IngestMeasurementsResult> Handle(
        IngestMeasurementsCommand request,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);

        try
        {
            // Resolve or create MeterPoint
            var meterPoint = await unitOfWork.MeterPoints
                .GetByMeterPointNumberAsync(request.MeterPointNumber, cancellationToken);

            if (meterPoint is null)
            {
                meterPoint = MeterPoint.Create(request.MeterPointNumber);
                await unitOfWork.MeterPoints.AddAsync(meterPoint, cancellationToken);
                logger.LogInformation(
                    "Created new MeterPoint {MeterPointNumber} with Id {MeterPointId}",
                    request.MeterPointNumber, meterPoint.Id);
            }
            else
            {
                logger.LogDebug(
                    "Resolved existing MeterPoint {MeterPointNumber} with Id {MeterPointId}",
                    request.MeterPointNumber, meterPoint.Id);
            }

            // Idempotency: find timestamps already persisted
            var incomingTimestamps = request.Measurements
                .Select(m => m.Timestamp.ToUniversalTime())
                .ToList();

            var existingTimestamps = await unitOfWork.Measurements
                .GetExistingTimestampsAsync(meterPoint.Id, incomingTimestamps, cancellationToken);

            // Build new measurements, skipping already-existing ones
            var newMeasurements = request.Measurements
                .Where(m => !existingTimestamps.Contains(m.Timestamp.ToUniversalTime()))
                .Select(m => MeterMeasurement.Create(
                    meterPoint.Id,
                    m.Timestamp,
                    m.TotalConsumption,
                    m.GridShare,
                    m.CommunityShare,
                    m.GridSharePercent,
                    m.CommunitySharePercent,
                    m.Source))
                .ToList();

            if (newMeasurements.Count > 0)
            {
                await unitOfWork.Measurements.BulkInsertAsync(newMeasurements, cancellationToken);
            }

            await unitOfWork.CommitAsync(cancellationToken);

            var result = new IngestMeasurementsResult(
                meterPoint.Id,
                request.Measurements.Count,
                newMeasurements.Count,
                existingTimestamps.Count);

            logger.LogInformation(
                "Ingested measurements for {MeterPointNumber}: {Inserted} inserted, {Skipped} skipped",
                request.MeterPointNumber, result.Inserted, result.Skipped);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest measurements for {MeterPointNumber}", request.MeterPointNumber);
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
