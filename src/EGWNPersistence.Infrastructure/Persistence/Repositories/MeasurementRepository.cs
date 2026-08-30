using Dapper;
using EGWNPersistence.Domain.Entities;
using EGWNPersistence.Domain.Interfaces;
using Npgsql;
using NpgsqlTypes;

namespace EGWNPersistence.Infrastructure.Persistence.Repositories;

public sealed class MeasurementRepository(NpgsqlConnection connection, NpgsqlTransaction? transaction)
    : IMeasurementRepository
{
    public async Task<IReadOnlySet<DateTimeOffset>> GetExistingTimestampsAsync(
        Guid meterPointId,
        IEnumerable<DateTimeOffset> timestamps,
        CancellationToken cancellationToken = default)
    {
        var tsArray = timestamps.Select(t => t.ToUniversalTime()).ToArray();
        if (tsArray.Length == 0)
            return new HashSet<DateTimeOffset>();

        const string sql = """
            SELECT timestamp
            FROM raw_measurements
            WHERE meter_point_id = @MeterPointId
              AND timestamp = ANY(@Timestamps)
            """;

        var cmd = new CommandDefinition(
            sql,
            new { MeterPointId = meterPointId, Timestamps = tsArray },
            transaction,
            cancellationToken: cancellationToken);

        var existing = await connection.QueryAsync<DateTimeOffset>(cmd);
        return existing.ToHashSet();
    }

    public async Task BulkInsertAsync(
        IEnumerable<MeterMeasurement> measurements,
        CancellationToken cancellationToken = default)
    {
        var list = measurements.ToList();
        if (list.Count == 0)
            return;

        // Use Npgsql binary COPY for maximum write throughput on TimescaleDB
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY raw_measurements
              (id, meter_point_id, timestamp, total_consumption, grid_share, community_share,
               grid_share_percent, community_share_percent, source, ingested_at, created_at)
            FROM STDIN (FORMAT BINARY)
            """,
            cancellationToken);

        foreach (var m in list)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(m.Id, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(m.MeterPointId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(m.Timestamp.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken);
            await writer.WriteAsync(m.TotalConsumption, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(m.GridShare, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(m.CommunityShare, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(m.GridSharePercent, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(m.CommunitySharePercent, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(m.Source, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(m.IngestedAt.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken);
            await writer.WriteAsync(m.CreatedAt.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }
}
