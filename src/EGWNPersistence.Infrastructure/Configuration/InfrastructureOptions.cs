namespace EGWNPersistence.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; init; } = string.Empty;
}

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "/";
    public string Username { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string QueueName { get; init; } = "egwn.measurements";
    public string DeadLetterExchange { get; init; } = "egwn.dlx";
    public string DeadLetterQueue { get; init; } = "egwn.measurements.dlq";
    public ushort PrefetchCount { get; init; } = 10;
}
