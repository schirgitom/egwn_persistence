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

public sealed class KpiConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KpiConsumer> _logger;
    private readonly RabbitMqOptions _options;
    private readonly RabbitMqQueueOptions _queue;
    private IConnection? _connection;
    private IChannel? _channel;

    public KpiConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<KpiConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _queue = _options.Kpis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeAsync(stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _logger.LogInformation("KpiConsumer shutting down.");
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

        _connection = await factory.CreateConnectionAsync("egwn-persistence-kpi", cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            _options.DeadLetterExchange,
            ExchangeType.Fanout,
            durable: true,
            cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            _queue.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            _queue.DeadLetterQueue,
            _options.DeadLetterExchange,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            _options.Exchange,
            _options.ExchangeType,
            durable: true,
            cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            _queue.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchange
            },
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            _queue.QueueName,
            _options.Exchange,
            _queue.RoutingKey,
            cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(0, _options.PrefetchCount, false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleMessageAsync;

        await _channel.BasicConsumeAsync(
            _queue.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "RabbitMQ KPI consumer started. Queue '{Queue}' bound to exchange '{Exchange}' ({ExchangeType}) with routing key '{RoutingKey}' at {Host}:{Port}",
            _queue.QueueName, _options.Exchange, _options.ExchangeType, _queue.RoutingKey, _options.Host, _options.Port);
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        var deliveryTag = args.DeliveryTag;
        string? communityId = null;

        try
        {
            var body = Encoding.UTF8.GetString(args.Body.Span);
            var dto = JsonSerializer.Deserialize<KpiMessageDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dto is null)
            {
                _logger.LogWarning("Received null/empty KPI message. Dead-lettering.");
                await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
                return;
            }

            communityId = dto.CommunityId;
            _logger.LogDebug(
                "Received KPI message for community {CommunityId} with {Count} entries",
                communityId, dto.Values.Count);

            var command = KpiMessageMapper.ToCommand(dto);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(command);

            _logger.LogInformation(
                "KPI message processed for {CommunityId}: {Inserted} inserted, {Updated} updated",
                communityId, result.Inserted, result.Updated);

            await _channel!.BasicAckAsync(deliveryTag, multiple: false);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex,
                "Validation failed for KPI message (Community: {CommunityId}). Dead-lettering.",
                communityId);
            await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize KPI message. Dead-lettering.");
            await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing KPI message for {CommunityId}. Requeueing.",
                communityId);
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
