using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace EGWNPersistence.Worker.Provisioning;

public sealed record MeterEntitlement(string CommunityId, string MeterPointNumber);
public sealed record CustomerEntitlements(string[] Communities, MeterEntitlement[] Meters);

public sealed class CustomerProvisioner(NpgsqlDataSource dataSource)
{
    public async Task<string> ProvisionAsync(Guid customerId, string password, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT grafana_access.provision_customer(@customerId, @password)");
        command.Parameters.AddWithValue("customerId", NpgsqlDbType.Uuid, customerId);
        command.Parameters.AddWithValue("password", NpgsqlDbType.Text, password);
        return (string)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Provisioning returned no database role."));
    }

    public async Task ReplaceEntitlementsAsync(
        Guid customerId, string[] communities, MeterEntitlement[] meters, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT grafana_access.replace_entitlements(@customerId, @communities, @meters)");
        command.Parameters.AddWithValue("customerId", NpgsqlDbType.Uuid, customerId);
        command.Parameters.AddWithValue("communities", NpgsqlDbType.Array | NpgsqlDbType.Text, communities);
        command.Parameters.AddWithValue("meters", NpgsqlDbType.Jsonb,
            JsonSerializer.Serialize(meters, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DisableAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT grafana_access.disable_customer(@customerId)");
        command.Parameters.AddWithValue("customerId", NpgsqlDbType.Uuid, customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
