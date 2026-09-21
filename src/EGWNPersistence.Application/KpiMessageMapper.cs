using EGWNPersistence.Application.Commands;
using EGWNPersistence.Application.DTOs;

namespace EGWNPersistence.Application;

/// <summary>
/// Maps between the RabbitMQ KPI DTO and the application command.
/// </summary>
public static class KpiMessageMapper
{
    public static IngestKpisCommand ToCommand(KpiMessageDto dto)
    {
        var entries = dto.Values
            .Select(v => new KpiEntry(
                v.Timestamp,
                v.Values.Autarky,
                v.Values.OwnConsumption,
                v.Values.Community,
                v.Values.Feed,
                v.Values.RemainingDemand))
            .ToList();

        return new IngestKpisCommand(dto.CommunityId, entries);
    }
}
