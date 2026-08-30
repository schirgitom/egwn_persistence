using EGWNPersistence.Application.Commands;
using EGWNPersistence.Application.DTOs;

namespace EGWNPersistence.Application;

/// <summary>
/// Maps between the RabbitMQ DTO and the application command.
/// </summary>
public static class MeasurementMapper
{
    public static IngestMeasurementsCommand ToCommand(MeasurementMessageDto dto)
    {
        var entries = dto.Measurements
            .Select(m => new MeasurementEntry(
                m.Timestamp,
                m.TotalConsumption,
                m.GridShare,
                m.CommunityShare,
                m.GridSharePercent,
                m.CommunitySharePercent,
                m.Source))
            .ToList();

        return new IngestMeasurementsCommand(dto.MeterPointNumber, entries);
    }
}
