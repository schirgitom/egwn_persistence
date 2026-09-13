using EGWNPersistence.Application.Commands;
using EGWNPersistence.Application.DTOs;

namespace EGWNPersistence.Application;

/// <summary>
/// Maps between the RabbitMQ DTO and the application command.
/// </summary>
public static class MeasurementMapper
{
    public static IngestMeasurementsCommand ToCommand(MeterReadingMessageDto dto)
    {
        var entries = dto.Readings
            .Select(m =>
            {
                var total = m.TotalConsumptionValue;
                var gridPercent = total == 0 ? 0m : m.GridShareValue / total;
                var communityPercent = total == 0 ? 0m : m.CommunityShareValue / total;

                return new MeasurementEntry(
                    m.Timestamp,
                    total,
                    m.GridShareValue,
                    m.CommunityShareValue,
                    gridPercent,
                    communityPercent,
                    "EDA");
            })
            .ToList();

        return new IngestMeasurementsCommand(dto.MeterPointNumber, entries);
    }
}
