using Dapper;
using EGWNPersistence.Domain.Aggregates;
using EGWNPersistence.Domain.Interfaces;
using Npgsql;

namespace EGWNPersistence.Infrastructure.Persistence.Repositories;

public sealed class MeterPointRepository(NpgsqlConnection connection, NpgsqlTransaction? transaction)
    : IMeterPointRepository
{
    public async Task<MeterPoint?> GetByMeterPointNumberAsync(
        string meterPointNumber,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, meter_point_number, created_at, updated_at
            FROM meter_points
            WHERE meter_point_number = @MeterPointNumber
            LIMIT 1
            """;

        var cmd = new CommandDefinition(
            sql,
            new { MeterPointNumber = meterPointNumber },
            transaction,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<MeterPointRow>(cmd);
        return row is null
            ? null
            : MeterPoint.Reconstitute(row.id, row.meter_point_number, row.created_at, row.updated_at);
    }

    public async Task AddAsync(MeterPoint meterPoint, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO meter_points (id, meter_point_number, created_at, updated_at)
            VALUES (@Id, @MeterPointNumber, @CreatedAt, @UpdatedAt)
            ON CONFLICT (meter_point_number) DO NOTHING
            """;

        var cmd = new CommandDefinition(
            sql,
            new
            {
                Id = meterPoint.Id,
                MeterPointNumber = meterPoint.MeterPointNumber,
                CreatedAt = meterPoint.CreatedAt,
                UpdatedAt = meterPoint.UpdatedAt
            },
            transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(cmd);
    }

    private sealed record MeterPointRow(
        Guid id,
        string meter_point_number,
        DateTimeOffset created_at,
        DateTimeOffset updated_at);
}
