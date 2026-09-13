using System.Text;
using System.Text.Json;
using EGWNPersistence.Application;
using EGWNPersistence.Application.DTOs;
using EGWNPersistence.Infrastructure.Configuration;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EGWNPersistence.Infrastructure.Messaging.RabbitMQ;

public sealed class MeasurementConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MeasurementConsumer> _logger;
    private readonly RabbitMqOptions _options;
    private IConnection? _connection;
    private IChannel? _channel;

    public MeasurementConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<MeasurementConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeAsync(stoppingToken);
        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _logger.LogInformation("MeasurementConsumer shutting down.");
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.Username,
            Password = _options.Password,
            AutomaticRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync("egwn-persistence", cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Declare dead-letter infrastructure
        await _channel.ExchangeDeclareAsync(
            _options.DeadLetterExchange,
            ExchangeType.Fanout,
            durable: true,
            cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            _options.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            _options.DeadLetterQueue,
            _options.DeadLetterExchange,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            _options.Exchange,
            _options.ExchangeType,
            durable: true,
            cancellationToken: cancellationToken);

        // Declare main queue with DLX routing
        await _channel.QueueDeclareAsync(
            _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchange
            },
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            _options.QueueName,
            _options.Exchange,
            _options.RoutingKey,
            cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(0, _options.PrefetchCount, false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleMessageAsync;

        await _channel.BasicConsumeAsync(
            _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "RabbitMQ consumer started. Queue '{Queue}' bound to exchange '{Exchange}' ({ExchangeType}) with routing key '{RoutingKey}' at {Host}:{Port}",
            _options.QueueName, _options.Exchange, _options.ExchangeType, _options.RoutingKey, _options.Host, _options.Port);
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        var deliveryTag = args.DeliveryTag;
        string? meterPointNumber = null;

        try
        {
            var body = Encoding.UTF8.GetString(args.Body.Span);
            var dto = JsonSerializer.Deserialize<MeterReadingMessageDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto is null)
            {
                _logger.LogWarning("Received null/empty measurement message. Dead-lettering.");
                await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
                return;
            }

            meterPointNumber = dto.MeterPointNumber;
            _logger.LogDebug(
                "Received measurement message for {MeterPointNumber} with {Count} entries",
                meterPointNumber, dto.Readings.Count);

            var command = MeasurementMapper.ToCommand(dto);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(command);

            _logger.LogInformation(
                "Message processed for {MeterPointNumber}: {Inserted} inserted, {Skipped} skipped",
                meterPointNumber, result.Inserted, result.Skipped);

            await _channel!.BasicAckAsync(deliveryTag, multiple: false);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex,
                "Validation failed for message (MeterPoint: {MeterPointNumber}). Dead-lettering.",
                meterPointNumber);
            // Validation failures are unrecoverable — send to DLQ
            await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message. Dead-lettering.");
            await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing message for {MeterPointNumber}. Requeueing.",
                meterPointNumber);
            // Transient failures (DB down, etc.) — requeue for retry
            await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}
