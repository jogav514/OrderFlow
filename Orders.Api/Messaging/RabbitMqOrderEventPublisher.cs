using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts.Events;
using OrderFlow.Contracts.Messaging;
using RabbitMQ.Client;

namespace Orders.Api.Messaging;

/// <summary>
/// Implementación de IOrderEventPublisher usando RabbitMQ.Client.
/// Declara el exchange y las colas durables y publica eventos serializados en JSON.
/// </summary>
public class RabbitMqOrderEventPublisher : IOrderEventPublisher
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqOrderEventPublisher> _logger;

    public RabbitMqOrderEventPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqOrderEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishOrderCreatedAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            Password = _options.Password
        };

        // Creamos conexión y canal para publicar de forma segura y confirmar entrega
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // Declaración idempotente: asegura que el exchange y la cola existan y sean durables
        channel.ExchangeDeclare(
            exchange: RabbitMqTopology.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: RabbitMqTopology.OrderCreatedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        channel.QueueBind(
            queue: RabbitMqTopology.OrderCreatedQueue,
            exchange: RabbitMqTopology.ExchangeName,
            routingKey: RabbitMqTopology.OrderCreatedRoutingKey);

        var json = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true; // El mensaje se escribe en disco dentro de RabbitMQ
        properties.ContentType = "application/json";
        properties.MessageId = @event.EventId.ToString();

        channel.BasicPublish(
            exchange: RabbitMqTopology.ExchangeName,
            routingKey: RabbitMqTopology.OrderCreatedRoutingKey,
            basicProperties: properties,
            body: body);

        _logger.LogInformation(
            "Evento OrderCreated publicado en RabbitMQ. EventId={EventId}, OrderId={OrderId}",
            @event.EventId, @event.OrderId);

        return Task.CompletedTask;
    }
}