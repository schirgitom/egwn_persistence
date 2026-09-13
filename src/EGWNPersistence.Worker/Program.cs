using EGWNPersistence.Application;
using EGWNPersistence.Infrastructure;
using EGWNPersistence.Infrastructure.Configuration;
using EGWNPersistence.Infrastructure.Persistence;
using EGWNPersistence.Worker.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("RabbitMQ", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EGWNPersistence Worker");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Configuration
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    var consulAddress = builder.Configuration["Consul:Address"];
    var consulKeyPrefix = builder.Configuration["Consul:KeyPrefix"] ?? "egwn/persistence";
    if (!string.IsNullOrWhiteSpace(consulAddress))
    {
        builder.Configuration.AddConsul(consulAddress, consulKeyPrefix);
    }

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

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

    await using (var scope = host.Services.CreateAsyncScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<EGWNPersistenceDbContext>();
        Log.Information("Applying EF Core migrations at startup");
        await dbContext.Database.MigrateAsync();
        Log.Information("EF Core migrations applied successfully");
    }

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
