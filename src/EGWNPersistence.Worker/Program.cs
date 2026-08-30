using EGWNPersistence.Application;
using EGWNPersistence.Infrastructure;
using EGWNPersistence.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("RabbitMQ", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EGWNPersistence Worker");

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter()));

    // Application + Infrastructure layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Health checks
    var dbConnStr = builder.Configuration["Database:ConnectionString"]
        ?? throw new InvalidOperationException("Database:ConnectionString is required.");

    var rabbitOptions = builder.Configuration
        .GetSection(RabbitMqOptions.SectionName)
        .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

    builder.Services.AddHealthChecks()
        .AddNpgSql(
            dbConnStr,
            name: "timescaledb",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["db", "timescale"])
        .AddRabbitMQ(
            sp =>
            {
                var factory = new ConnectionFactory
                {
                    HostName = rabbitOptions.Host,
                    Port = rabbitOptions.Port,
                    VirtualHost = rabbitOptions.VirtualHost,
                    UserName = rabbitOptions.Username,
                    Password = rabbitOptions.Password
                };
                return factory.CreateConnectionAsync("egwn-healthcheck").GetAwaiter().GetResult();
            },
            name: "rabbitmq",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["messaging", "rabbitmq"]);

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
