using EGWNPersistence.Application.Configuration;
using EGWNPersistence.Domain.Interfaces;
using EGWNPersistence.Infrastructure.Configuration;
using EGWNPersistence.Infrastructure.Messaging.RabbitMQ;
using EGWNPersistence.Infrastructure.Persistence;
using EGWNPersistence.Infrastructure.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.Configure<MeasurementCorrectionOptions>(configuration.GetSection(MeasurementCorrectionOptions.SectionName));

        var connectionString = configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        services.AddDbContext<EGWNPersistenceDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();

        // RabbitMQ consumer (hosted service)
        services.AddHostedService<MeasurementConsumer>();

        return services;
    }
}
