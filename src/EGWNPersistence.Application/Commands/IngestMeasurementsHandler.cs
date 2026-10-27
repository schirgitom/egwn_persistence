using EGWNPersistence.Application.Configuration;
using EGWNPersistence.Domain.Aggregates;
using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EGWNPersistence.Application.Commands;

public sealed class IngestMeasurementsHandler(
    IUnitOfWork unitOfWork,
    ILogger<IngestMeasurementsHandler> logger,
    IOptions<MeasurementCorrectionOptions> correctionOptions)
    : IRequestHandler<IngestMeasurementsCommand, IngestMeasurementsResult>
{
    private readonly TimeSpan _correctionWindow = correctionOptions.Value.ToTimeSpan();

    public async Task<IngestMeasurementsResult> Handle(
        IngestMeasurementsCommand request,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);

        try
        {
            var communityId = request.CommunityId.Trim();

            // Resolve or create MeterPoint
            var meterPoint = await unitOfWork.MeterPoints
                .GetByMeterPointNumberAsync(request.MeterPointNumber, cancellationToken);

            if (meterPoint is null)
            {
                meterPoint = MeterPoint.Create(request.MeterPointNumber);
                await unitOfWork.MeterPoints.AddAsync(meterPoint, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                if (meterPoint.Id <= 0)
                {
                    throw new InvalidOperationException(
                        $"Failed to persist MeterPoint '{request.MeterPointNumber}'. Generated meter ID is invalid: {meterPoint.Id}.");
                }

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

            if (meterPoint.Id <= 0)
            {
                throw new InvalidOperationException(
                    $"MeterPoint '{request.MeterPointNumber}' has invalid ID {meterPoint.Id}. Aborting measurement write.");
            }

            await unitOfWork.MeterPoints.EnsureCommunityMembershipAsync(
                meterPoint.Id, communityId, cancellationToken);

            var incomingTimestamps = request.Measurements
                .Select(m => m.Timestamp.ToUniversalTime())
                .ToList();

            var existingMeasurements = await unitOfWork.Measurements
                .GetExistingAsync(meterPoint.Id, communityId, incomingTimestamps, cancellationToken);

            var newMeasurements = new List<MeterMeasurement>();
            var correctedMeasurements = new List<MeterMeasurement>();
            var skipped = 0;
            var utcNow = DateTimeOffset.UtcNow;

            foreach (var entry in request.Measurements)
            {
                var timestampUtc = entry.Timestamp.ToUniversalTime();
                if (existingMeasurements.TryGetValue(timestampUtc, out var existingMeasurement))
                {
                    if (existingMeasurement.CanBeCorrected(utcNow, _correctionWindow))
                    {
                        existingMeasurement.ApplyCorrection(
                            entry.TotalConsumption,
                            entry.GridShare,
                            entry.CommunityShare,
                            entry.GridSharePercent,
                            entry.CommunitySharePercent,
                            entry.Source,
                            utcNow,
                            utcNow,
                            _correctionWindow);
                        correctedMeasurements.Add(existingMeasurement);
                        continue;
                    }

                    skipped++;
                    continue;
                }

                newMeasurements.Add(MeterMeasurement.Create(
                    meterPoint.Id,
                    communityId,
                    entry.Timestamp,
                    entry.TotalConsumption,
                    entry.GridShare,
                    entry.CommunityShare,
                    entry.GridSharePercent,
                    entry.CommunitySharePercent,
                    entry.Source));
            }

            if (newMeasurements.Count > 0)
            {
                await unitOfWork.Measurements.BulkInsertAsync(newMeasurements, cancellationToken);
            }

            if (correctedMeasurements.Count > 0)
            {
                await unitOfWork.Measurements.UpdateAsync(correctedMeasurements, cancellationToken);
            }

            await unitOfWork.CommitAsync(cancellationToken);

            var result = new IngestMeasurementsResult(
                meterPoint.Id,
                request.Measurements.Count,
                newMeasurements.Count,
                request.Measurements.Count - newMeasurements.Count);

            logger.LogInformation(
                "Ingested measurements for {MeterPointNumber}: {Inserted} inserted, {Corrected} corrected, {Skipped} skipped",
                request.MeterPointNumber, result.Inserted, correctedMeasurements.Count, result.Skipped);

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
