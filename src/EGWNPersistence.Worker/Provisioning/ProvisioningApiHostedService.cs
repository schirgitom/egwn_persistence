using System.Security.Cryptography;
using System.Text;
using EGWNPersistence.Worker.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Serilog;

namespace EGWNPersistence.Worker.Provisioning;

public sealed class ProvisioningApiHostedService(IConfiguration configuration, IHostEnvironment environment)
    : IHostedService, IAsyncDisposable
{
    private WebApplication? _app;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Provisioning:Enabled"))
            return;

        var token = configuration["Provisioning:ServiceToken"];
        var connectionString = configuration["Provisioning:ConnectionString"];
        if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
            throw new InvalidOperationException("Provisioning:ServiceToken must be a secret of at least 32 characters.");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Provisioning:ConnectionString is required.");

        var port = configuration.GetValue("Provisioning:Port", 8443);
        if (port is < 1 or > 65535)
            throw new InvalidOperationException("Provisioning:Port must be between 1 and 65535.");

        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment.EnvironmentName,
            ContentRootPath = environment.ContentRootPath
        });
        builder.Configuration.AddConfiguration(configuration);
        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());
        builder.WebHost.ConfigureKestrel(options =>
            options.ListenAnyIP(port, listen => listen.UseHttps()));
        builder.Services.AddProblemDetails();
        builder.Services.AddControllers().AddApplicationPart(typeof(GrafanaCustomersController).Assembly);
        builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        builder.Services.AddSingleton<CustomerProvisioner>();

        var app = builder.Build();
        app.UseSerilogRequestLogging();
        app.UseExceptionHandler();
        app.Use(async (context, next) =>
        {
            if (!context.Request.IsHttps)
            {
                context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
                return;
            }

            var authorization = context.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
                authorization.Length > 4096 ||
                !CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(Encoding.UTF8.GetBytes(authorization["Bearer ".Length..])), tokenHash))
            {
                context.Response.Headers.WWWAuthenticate = "Bearer";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context);
        });

        app.MapControllers();

        _app = app;
        await app.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
            await _app.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
