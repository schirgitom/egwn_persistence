namespace EGWNPersistence.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; init; } = string.Empty;
}

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    // Shared connection settings
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "/";
    public string Username { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string Exchange { get; init; } = "eda.sync";
    public string ExchangeType { get; init; } = "topic";
    public string DeadLetterExchange { get; init; } = "egwn.dlx";
    public ushort PrefetchCount { get; init; } = 10;

    // Per-consumer queue configuration
    public RabbitMqQueueOptions Measurements { get; init; } = new()
    {
        RoutingKey = "eda.sync.result",
        QueueName = "egwn.measurements",
        DeadLetterQueue = "egwn.measurements.dlq"
    };

    public RabbitMqQueueOptions Kpis { get; init; } = new()
    {
        RoutingKey = "eda.sync.kpi",
        QueueName = "egwn.kpis",
        DeadLetterQueue = "egwn.kpis.dlq"
    };
}

public sealed class RabbitMqQueueOptions
{
    public string RoutingKey { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
    public string DeadLetterQueue { get; init; } = string.Empty;
}
