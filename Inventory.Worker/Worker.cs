using System.Text;
using System.Text.Json;
using Inventory.Worker.Messaging;
using Inventory.Worker.Services;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts.Events;
using OrderFlow.Contracts.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Inventory.Worker;

/// <summary>
/// Servicio en segundo plano que escucha eventos OrderCreated desde RabbitMQ,
/// delega el descuento de stock a IInventoryProcessor y publica StockReserved o StockRejected.
/// </summary>
public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<Worker> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public Worker(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Reintentamos conectar por si RabbitMQ aún está arrancando en Docker
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
                    "No se pudo conectar a RabbitMQ. Reintentando en 5 segundos...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        // Mantiene vivo el BackgroundService mientras no se solicite apagado
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Inventory.Worker deteniéndose de forma ordenada.");
        }
    }

    private void ConnectAndConsume(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.UserName) ||
            string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "Faltan variables de entorno de RabbitMQ (RabbitMq__Host, RabbitMq__UserName, RabbitMq__Password).");
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

        // Declaramos el exchange y las 3 colas durables del sistema
        _channel.ExchangeDeclare(RabbitMqTopology.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);

        DeclareAndBindQueue(RabbitMqTopology.OrderCreatedQueue, RabbitMqTopology.OrderCreatedRoutingKey);
        DeclareAndBindQueue(RabbitMqTopology.StockReservedQueue, RabbitMqTopology.StockReservedRoutingKey);
        DeclareAndBindQueue(RabbitMqTopology.StockRejectedQueue, RabbitMqTopology.StockRejectedRoutingKey);

        // Procesa 1 mensaje a la vez antes de pedir el siguiente (control de carga)
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) => await HandleMessageAsync(ea, stoppingToken);

        _channel.BasicConsume(
            queue: RabbitMqTopology.OrderCreatedQueue,
            autoAck: false, // Confirmación manual para no perder mensajes si falla el proceso
            consumer: consumer);

        _logger.LogInformation("Inventory.Worker conectado y escuchando la cola '{Queue}'.",
            RabbitMqTopology.OrderCreatedQueue);
    }

    private void DeclareAndBindQueue(string queueName, string routingKey)
    {
        _channel!.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(queue: queueName, exchange: RabbitMqTopology.ExchangeName, routingKey: routingKey);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        try
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            var orderCreated = JsonSerializer.Deserialize<OrderCreatedEvent>(body);

            if (orderCreated is null)
            {
                _logger.LogError("Mensaje vacío o inválido en '{Queue}'. Descartando.",
                    RabbitMqTopology.OrderCreatedQueue);
                _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            // Creamos un Scope nuevo para obtener DbContext e IInventoryProcessor de forma segura
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IInventoryProcessor>();

            var result = await processor.ProcessOrderCreatedAsync(orderCreated, stoppingToken);

            // Publicamos el evento de respuesta correspondiente (si no fue duplicado)
            if (result.Outcome == ProcessOutcome.Reserved && result.ReservedEvent is not null)
            {
                PublishEvent(result.ReservedEvent, RabbitMqTopology.StockReservedRoutingKey, result.ReservedEvent.EventId);
            }
            else if (result.Outcome == ProcessOutcome.Rejected && result.RejectedEvent is not null)
            {
                PublishEvent(result.RejectedEvent, RabbitMqTopology.StockRejectedRoutingKey, result.RejectedEvent.EventId);
            }

            // Confirmamos a RabbitMQ que el mensaje quedó procesado exitosamente
            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (JsonException ex)
        {
            // Mensaje malformado: no tiene sentido reencolarlo
            _logger.LogError(ex, "Error de deserialización JSON. Descartando mensaje.");
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            // Error técnico (ej. SQL Server temporalmente caído): reencolamos tras breve pausa
            _logger.LogError(ex, "Error técnico procesando OrderCreated. Se reencolará el mensaje.");
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private void PublishEvent<TEvent>(TEvent @event, string routingKey, Guid eventId)
    {
        var json = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(json);

        var props = _channel!.CreateBasicProperties();
        props.Persistent = true;
        props.ContentType = "application/json";
        props.MessageId = eventId.ToString();

        _channel.BasicPublish(
            exchange: RabbitMqTopology.ExchangeName,
            routingKey: routingKey,
            basicProperties: props,
            body: body);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}