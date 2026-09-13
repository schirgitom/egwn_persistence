using System.Text;
using System.Text.Json;
using Consul;
using Microsoft.Extensions.Configuration;

namespace EGWNPersistence.Worker.Configuration;

public static class ConsulConfigurationExtensions
{
    public static IConfigurationBuilder AddConsul(
        this IConfigurationBuilder builder,
        string address,
        string keyPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);

        return builder.Add(new ConsulConfigurationSource(address, keyPrefix));
    }
}

public sealed class ConsulConfigurationSource(string address, string keyPrefix) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new ConsulConfigurationProvider(address, keyPrefix);
    }
}

public sealed class ConsulConfigurationProvider(string address, string keyPrefix) : ConfigurationProvider
{
    public override void Load()
    {
        var client = new ConsulClient(config =>
        {
            config.Address = new Uri(address);
        });

        try
        {
            var entries = client.KV.List(keyPrefix.Trim('/')).GetAwaiter().GetResult();
            var pairs = entries.Response ?? [];

            foreach (var pair in pairs)
            {
                var key = pair.Key ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key) || key == keyPrefix.Trim('/'))
                    continue;

                var cleanKey = key.Trim('/');
                var prefix = keyPrefix.Trim('/');
                if (cleanKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    cleanKey = cleanKey[prefix.Length..].Trim('/');
                }

                if (string.IsNullOrWhiteSpace(cleanKey))
                    continue;

                cleanKey = cleanKey.Replace('/', ':');
                cleanKey = cleanKey.Replace('\\', ':');

                var value = pair.Value is null ? string.Empty : Encoding.UTF8.GetString(pair.Value);
                if (TryParseJsonObject(value, out var json))
                {
                    AddJsonSection(cleanKey, json);
                    continue;
                }

                Data[cleanKey] = value;
            }
        }
        catch (Exception)
        {
            // Consul not available: local appsettings continue to work.
        }
    }

    private static bool TryParseJsonObject(string value, out JsonElement json)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            json = default;
            return false;
        }

        try
        {
            json = JsonDocument.Parse(value).RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            json = default;
            return false;
        }
    }

    private void AddJsonSection(string key, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var nextKey = string.IsNullOrEmpty(key)
                        ? property.Name
                        : $"{key}:{property.Name}";
                    AddJsonSection(nextKey, property.Value);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    AddJsonSection($"{key}:{index++}", item);
                }
                break;
            case JsonValueKind.String:
                Data[key] = element.GetString() ?? string.Empty;
                break;
            case JsonValueKind.Number:
                Data[key] = element.GetRawText();
                break;
            case JsonValueKind.True:
                Data[key] = bool.TrueString;
                break;
            case JsonValueKind.False:
                Data[key] = bool.FalseString;
                break;
            case JsonValueKind.Null:
                Data[key] = string.Empty;
                break;
            default:
                break;
        }
    }
}
