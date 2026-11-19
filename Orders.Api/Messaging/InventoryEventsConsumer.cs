using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts.Events;
using OrderFlow.Contracts.Messaging;
using Orders.Api.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orders.Api.Messaging;

/// <summary>
/// Consumidor en segundo plano dentro de Orders.Api que escucha StockReserved y StockRejected
/// para completar el ciclo de vida del pedido.
/// </summary>
public class InventoryEventsConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<InventoryEventsConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public InventoryEventsConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<InventoryEventsConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ConnectAndConsume(stoppingToken);
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "Orders.Api no pudo conectar al consumidor de RabbitMQ. Reintentando en 5 segundos...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("InventoryEventsConsumer deteniéndose.");
        }
    }

    private void ConnectAndConsume(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.UserName) ||
            string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "Faltan variables de entorno de RabbitMQ en Orders.Api.");
        }

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            AutomaticRecoveryEnabled = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(RabbitMqTopology.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);

        DeclareAndBindQueue(RabbitMqTopology.StockReservedQueue, RabbitMqTopology.StockReservedRoutingKey);
        DeclareAndBindQueue(RabbitMqTopology.StockRejectedQueue, RabbitMqTopology.StockRejectedRoutingKey);

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        // Consumidor para StockReserved -> Confirmed
        var reservedConsumer = new EventingBasicConsumer(_channel);
        reservedConsumer.Received += async (_, ea) => await HandleReservedAsync(ea, stoppingToken);
        _channel.BasicConsume(RabbitMqTopology.StockReservedQueue, autoAck: false, reservedConsumer);

        // Consumidor para StockRejected -> Rejected
        var rejectedConsumer = new EventingBasicConsumer(_channel);
        rejectedConsumer.Received += async (_, ea) => await HandleRejectedAsync(ea, stoppingToken);
        _channel.BasicConsume(RabbitMqTopology.StockRejectedQueue, autoAck: false, rejectedConsumer);

        _logger.LogInformation("Orders.Api escuchando colas '{Reserved}' y '{Rejected}'.",
            RabbitMqTopology.StockReservedQueue, RabbitMqTopology.StockRejectedQueue);
    }

    private void DeclareAndBindQueue(string queueName, string routingKey)
    {
        _channel!.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(queue: queueName, exchange: RabbitMqTopology.ExchangeName, routingKey: routingKey);
    }

    private async Task HandleReservedAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var @event = JsonSerializer.Deserialize<StockReservedEvent>(json);

            if (@event is null)
            {
                _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var updater = scope.ServiceProvider.GetRequiredService<IOrderStatusUpdater>();
            await updater.ApplyStockReservedAsync(@event, stoppingToken);

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON inválido en StockReserved.");
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error técnico procesando StockReserved. Se reencolará.");
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private async Task HandleRejectedAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var @event = JsonSerializer.Deserialize<StockRejectedEvent>(json);

            if (@event is null)
            {
                _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var updater = scope.ServiceProvider.GetRequiredService<IOrderStatusUpdater>();
            await updater.ApplyStockRejectedAsync(@event, stoppingToken);

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON inválido en StockRejected.");
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error técnico procesando StockRejected. Se reencolará.");
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}