using EGWNPersistence.Domain.Interfaces;
using EGWNPersistence.Infrastructure.Configuration;
using EGWNPersistence.Infrastructure.Messaging.RabbitMQ;
using EGWNPersistence.Infrastructure.Persistence.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EGWNPersistence.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        // Npgsql data source (manages connection pooling)
        var connectionString = configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        var dataSource = new NpgsqlDataSourceBuilder(connectionString)
            .EnableDynamicJson()
            .Build();

        services.AddSingleton(dataSource);

        // Unit of Work — scoped per request/message
        services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();

        // RabbitMQ consumer (hosted service)
        services.AddHostedService<MeasurementConsumer>();

        return services;
    }
}
